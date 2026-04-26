using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Abstractions.Storage;
using MuseSpace.Domain.Enums;
using MuseSpace.Infrastructure.Novels;
using Npgsql;
using NpgsqlTypes;
using System.Diagnostics;
using System.Text;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Hangfire Job 1：读取原著文件 → 文本切片 → Npgsql COPY 批量写入 novel_chunks。
/// 完成后由 Hangfire 链式触发 EmbedNovelJob。
/// </summary>
public sealed class ChunkNovelJob
{
    private readonly INovelRepository _novelRepo;
    private readonly IStorageService _storage;
    private readonly NovelTextChunker _chunker;
    private readonly IConfiguration _configuration;
    private readonly IImportProgressNotifier _notifier;
    private readonly ILogger<ChunkNovelJob> _logger;

    public ChunkNovelJob(
        INovelRepository novelRepo,
        IStorageService storage,
        NovelTextChunker chunker,
        IConfiguration configuration,
        IImportProgressNotifier notifier,
        ILogger<ChunkNovelJob> logger)
    {
        _novelRepo = novelRepo;
        _storage = storage;
        _chunker = chunker;
        _configuration = configuration;
        _notifier = notifier;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(Guid novelId, PerformContext? context)
    {
        _logger.LogInformation("ChunkNovelJob started for novel {NovelId}", novelId);

        var novel = await _novelRepo.GetByIdAsync(novelId);
        if (novel is null)
        {
            _logger.LogWarning("Novel {NovelId} not found, skipping", novelId);
            return;
        }

        novel.Status = NovelStatus.Chunking;
        novel.ProgressDone = 0;
        novel.ProgressTotal = 0;
        novel.LastError = null;
        novel.FinishedAt = null;
        novel.StartedAt ??= DateTime.UtcNow;
        novel.UpdatedAt = DateTime.UtcNow;
        await _novelRepo.UpdateAsync(novel);

        try
        {
            var stopwatch = Stopwatch.StartNew();

            // 1. Read file content
            string content;
            await using (var stream = await _storage.OpenReadAsync(novel.FileKey!))
            using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
            {
                content = await reader.ReadToEndAsync();
            }

            _logger.LogInformation("ChunkNovelJob read file for novel {NovelId}: {Length} chars in {ElapsedMs} ms",
                novelId, content.Length, stopwatch.ElapsedMilliseconds);

            // 2. Split into chunks
            var chunks = _chunker.Split(content, novelId, novel.StoryProjectId);
            _logger.LogInformation("Novel {NovelId} split into {Count} chunks", novelId, chunks.Count);
            await _notifier.NotifyChunkingProgressAsync(novelId, chunks.Count, chunks.Count);

            // 2b. 幂等保护：清除当前 novel 的旧 chunks（Hangfire 重试时避免重复）
            var connString = _configuration.GetConnectionString("DefaultConnection")!;
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            // 幂等：先删旧数据再 COPY（covering embeddings via FK or direct delete）
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM memory.chunk_embeddings WHERE \"ChunkId\" IN (SELECT \"Id\" FROM novel_chunks WHERE \"NovelId\" = $1)";
                cmd.Parameters.AddWithValue(novelId);
                await cmd.ExecuteNonQueryAsync();
            }
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM novel_chunks WHERE \"NovelId\" = $1";
                cmd.Parameters.AddWithValue(novelId);
                await cmd.ExecuteNonQueryAsync();
            }

            // 3. Bulk insert via PostgreSQL COPY BINARY
            await using var writer = await conn.BeginBinaryImportAsync(
                "COPY novel_chunks " +
                "(\"Id\", \"NovelId\", \"StoryProjectId\", \"ChunkIndex\", \"Content\", " +
                "\"CharCount\", \"TokenCount\", \"StartOffset\", \"EndOffset\", \"IsEmbedded\", \"CreatedAt\") " +
                "FROM STDIN (FORMAT BINARY)");

            foreach (var chunk in chunks)
            {
                try
                {
                    if (TryFindInvalidUnicodeOffset(chunk.Content, out var invalidOffset))
                    {
                        throw new InvalidOperationException(BuildInvalidChunkMessage(novelId, chunk, invalidOffset));
                    }

                    await writer.StartRowAsync();
                    await writer.WriteAsync(chunk.Id, NpgsqlDbType.Uuid);
                    await writer.WriteAsync(chunk.NovelId, NpgsqlDbType.Uuid);
                    await writer.WriteAsync(chunk.StoryProjectId, NpgsqlDbType.Uuid);
                    await writer.WriteAsync(chunk.ChunkIndex, NpgsqlDbType.Integer);
                    await writer.WriteAsync(chunk.Content, NpgsqlDbType.Text);
                    await writer.WriteAsync(chunk.CharCount, NpgsqlDbType.Integer);
                    await writer.WriteNullAsync(); // token_count (nullable)
                    await writer.WriteAsync(chunk.StartOffset, NpgsqlDbType.Integer);
                    await writer.WriteAsync(chunk.EndOffset, NpgsqlDbType.Integer);
                    await writer.WriteAsync(false, NpgsqlDbType.Boolean);
                    await writer.WriteAsync(DateTime.UtcNow, NpgsqlDbType.TimestampTz);
                }
                catch (Exception ex) when (ex is EncoderFallbackException or InvalidOperationException)
                {
                    TryFindInvalidUnicodeOffset(chunk.Content, out var invalidOffset);
                    _logger.LogError(
                        ex,
                        "Chunk write failed for novel {NovelId}, chunk {ChunkIndex}, source range [{StartOffset}, {EndOffset}), absolute offset {AbsoluteOffset}, preview: {Preview}",
                        novelId,
                        chunk.ChunkIndex,
                        chunk.StartOffset,
                        chunk.EndOffset,
                        invalidOffset >= 0 ? chunk.StartOffset + invalidOffset : chunk.StartOffset,
                        BuildContentPreview(chunk.Content, invalidOffset));
                    throw;
                }
            }

            await writer.CompleteAsync();

            // 4. Update novel status and chunk count
            novel.Status = NovelStatus.Embedding;
            novel.TotalChunks = chunks.Count;
            novel.ProgressDone = 0;
            novel.ProgressTotal = chunks.Count;
            novel.UpdatedAt = DateTime.UtcNow;
            await _novelRepo.UpdateAsync(novel);

            _logger.LogInformation(
                "ChunkNovelJob completed for novel {NovelId}: {Count} chunks written in {ElapsedMs} ms",
                novelId, chunks.Count, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChunkNovelJob failed for novel {NovelId}", novelId);
            novel.Status = NovelStatus.Failed;
            novel.LastError = ex.Message;
            novel.FinishedAt = DateTime.UtcNow;
            novel.UpdatedAt = DateTime.UtcNow;
            await _novelRepo.UpdateAsync(novel);
            await _notifier.NotifyImportFailedAsync(novelId, ex.Message);
            throw;
        }
    }

    private static bool TryFindInvalidUnicodeOffset(string value, out int offset)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                {
                    offset = i;
                    return true;
                }

                i++;
                continue;
            }

            if (char.IsLowSurrogate(value[i]))
            {
                offset = i;
                return true;
            }
        }

        offset = -1;
        return false;
    }

    private static string BuildInvalidChunkMessage(Guid novelId, Domain.Entities.NovelChunk chunk, int invalidOffset)
        => $"Invalid Unicode scalar detected before chunk import. NovelId={novelId}, ChunkIndex={chunk.ChunkIndex}, SourceRange=[{chunk.StartOffset}, {chunk.EndOffset}), AbsoluteOffset={chunk.StartOffset + invalidOffset}, Preview={BuildContentPreview(chunk.Content, invalidOffset)}";

    private static string BuildContentPreview(string content, int invalidOffset)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        var center = invalidOffset >= 0 ? invalidOffset : 0;
        var previewStart = Math.Max(0, center - 12);
        var previewLength = Math.Min(content.Length - previewStart, 24);
        return content.Substring(previewStart, previewLength)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
