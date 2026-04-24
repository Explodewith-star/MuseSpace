using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Abstractions.Storage;
using MuseSpace.Domain.Enums;
using MuseSpace.Infrastructure.Novels;
using Npgsql;
using NpgsqlTypes;

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
    private readonly ILogger<ChunkNovelJob> _logger;

    public ChunkNovelJob(
        INovelRepository novelRepo,
        IStorageService storage,
        NovelTextChunker chunker,
        IConfiguration configuration,
        ILogger<ChunkNovelJob> logger)
    {
        _novelRepo = novelRepo;
        _storage = storage;
        _chunker = chunker;
        _configuration = configuration;
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

        novel.Status = NovelStatus.Processing;
        novel.UpdatedAt = DateTime.UtcNow;
        await _novelRepo.UpdateAsync(novel);

        try
        {
            // 1. Read file content
            string content;
            await using (var stream = await _storage.OpenReadAsync(novel.FileKey!))
            using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
            {
                content = await reader.ReadToEndAsync();
            }

            // 2. Split into chunks
            var chunks = _chunker.Split(content, novelId, novel.StoryProjectId);
            _logger.LogInformation("Novel {NovelId} split into {Count} chunks", novelId, chunks.Count);

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

            await writer.CompleteAsync();

            // 4. Update novel status and chunk count
            novel.TotalChunks = chunks.Count;
            novel.UpdatedAt = DateTime.UtcNow;
            // Status stays Processing - EmbedNovelJob will complete it
            await _novelRepo.UpdateAsync(novel);

            _logger.LogInformation(
                "ChunkNovelJob completed for novel {NovelId}: {Count} chunks written",
                novelId, chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChunkNovelJob failed for novel {NovelId}", novelId);
            novel.Status = NovelStatus.Failed;
            novel.UpdatedAt = DateTime.UtcNow;
            await _novelRepo.UpdateAsync(novel);
            throw;
        }
    }
}
