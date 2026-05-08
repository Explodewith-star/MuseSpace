using Hangfire;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Enums;
using MuseSpace.Infrastructure.Persistence;
using MuseSpace.Infrastructure.Persistence.Entities;
using System.Diagnostics;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Hangfire Job 2：对 novel_chunks 逐批调用 IEmbeddingClient，
/// 将向量写入 memory.chunk_embeddings，并通过 SignalR 推送进度。
/// </summary>
public sealed class EmbedNovelJob
{
    private const int BatchSize = 50;

    private readonly INovelRepository _novelRepo;
    private readonly INovelChunkRepository _chunkRepo;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly MuseSpaceDbContext _db;
    private readonly IImportProgressNotifier _notifier;
    private readonly ITaskProgressService _taskProgress;
    private readonly ILogger<EmbedNovelJob> _logger;

    public EmbedNovelJob(
        INovelRepository novelRepo,
        INovelChunkRepository chunkRepo,
        IEmbeddingClient embeddingClient,
        MuseSpaceDbContext db,
        IImportProgressNotifier notifier,
        ITaskProgressService taskProgress,
        ILogger<EmbedNovelJob> logger)
    {
        _novelRepo = novelRepo;
        _chunkRepo = chunkRepo;
        _embeddingClient = embeddingClient;
        _db = db;
        _notifier = notifier;
        _taskProgress = taskProgress;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(Guid novelId, PerformContext? context)
    {
        _logger.LogInformation("EmbedNovelJob started for novel {NovelId}", novelId);

        var novel = await _novelRepo.GetByIdAsync(novelId);
        if (novel is null)
        {
            _logger.LogWarning("Novel {NovelId} not found, skipping", novelId);
            return;
        }

        // 查找项目所属用户（Hangfire 无 HTTP 上下文）
        var userId = await _db.StoryProjects
            .AsNoTracking()
            .Where(p => p.Id == novel.StoryProjectId)
            .Select(p => (Guid?)p.UserId)
            .FirstOrDefaultAsync();

        var bgTaskId = await _taskProgress.StartAsync(
            userId, novel.StoryProjectId, BackgroundTaskType.NovelImport,
            $"向量化《{novel.Title}》");

        try
        {
            var stopwatch = Stopwatch.StartNew();
            novel.Status = NovelStatus.Embedding;
            novel.LastError = null;
            novel.FinishedAt = null;
            novel.UpdatedAt = DateTime.UtcNow;
            await _novelRepo.UpdateAsync(novel);

            var chunks = await _chunkRepo.GetUnembeddedAsync(novelId);
            var total = chunks.Count;
            var done = 0;

            novel.ProgressDone = 0;
            novel.ProgressTotal = total;
            novel.UpdatedAt = DateTime.UtcNow;
            await _novelRepo.UpdateAsync(novel);

            _logger.LogInformation("EmbedNovelJob: {Total} chunks to embed for novel {NovelId}", total, novelId);

            // Process in batches to respect API rate limits
            for (int i = 0; i < chunks.Count; i += BatchSize)
            {
                var batch = chunks.Skip(i).Take(BatchSize).ToList();
                var vectors = await _embeddingClient.EmbedBatchAsync(batch.Select(chunk => chunk.Content).ToList());

                await using var transaction = await _db.Database.BeginTransactionAsync();

                for (var batchIndex = 0; batchIndex < batch.Count; batchIndex++)
                {
                    var chunk = batch[batchIndex];
                    var vector = vectors[batchIndex];
                    var embedding = new NovelChunkEmbedding
                    {
                        Id = Guid.NewGuid(),
                        ChunkId = chunk.Id,
                        StoryProjectId = chunk.StoryProjectId,
                        ModelName = _embeddingClient.ModelName,
                        Embedding = new Pgvector.Vector(vector),
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.ChunkEmbeddings.Add(embedding);
                }

                await _db.SaveChangesAsync();
                await _chunkRepo.MarkEmbeddedBatchAsync(batch.Select(chunk => chunk.Id));
                await transaction.CommitAsync();
                done += batch.Count;

                novel.ProgressDone = done;
                novel.ProgressTotal = total;
                novel.UpdatedAt = DateTime.UtcNow;
                await _novelRepo.UpdateAsync(novel);

                // Push progress via SignalR
                await _notifier.NotifyEmbedProgressAsync(novelId, done, total);
                var pct = total > 0 ? (int)(done * 100L / total) : 100;
                await _taskProgress.ReportProgressAsync(bgTaskId, pct, $"已向量化 {done}/{total} 段");

                _logger.LogInformation("EmbedNovelJob: {Done}/{Total} chunks embedded for novel {NovelId}",
                    done, total, novelId);
            }

            // Mark novel as fully indexed
            novel.Status = NovelStatus.Indexed;
            novel.ProgressDone = total;
            novel.ProgressTotal = total;
            novel.LastError = null;
            novel.FinishedAt = DateTime.UtcNow;
            novel.UpdatedAt = DateTime.UtcNow;
            await _novelRepo.UpdateAsync(novel);

            await _notifier.NotifyImportDoneAsync(novelId, total);
            await _taskProgress.CompleteAsync(bgTaskId, $"向量化完成，共 {total} 段");

            // 链式触发：自动资产提取
            BackgroundJob.Enqueue<ExtractNovelAssetsJob>(j => j.ExecuteAsync(novelId, null));

            _logger.LogInformation("EmbedNovelJob completed for novel {NovelId} in {ElapsedMs} ms", novelId, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmbedNovelJob failed for novel {NovelId}", novelId);
            novel.Status = NovelStatus.Failed;
            novel.LastError = ex.Message;
            novel.FinishedAt = DateTime.UtcNow;
            novel.UpdatedAt = DateTime.UtcNow;
            await _novelRepo.UpdateAsync(novel);
            await _notifier.NotifyImportFailedAsync(novelId, ex.Message);
            await _taskProgress.FailAsync(bgTaskId, ex.Message);
            throw;
        }
    }
}
