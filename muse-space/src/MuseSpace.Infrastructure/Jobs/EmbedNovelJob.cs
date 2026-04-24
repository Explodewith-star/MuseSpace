using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Enums;
using MuseSpace.Infrastructure.Persistence;
using MuseSpace.Infrastructure.Persistence.Entities;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Hangfire Job 2：对 novel_chunks 逐批调用 IEmbeddingClient，
/// 将向量写入 memory.chunk_embeddings，并通过 SignalR 推送进度。
/// </summary>
public sealed class EmbedNovelJob
{
    private const int BatchSize = 20;

    private readonly INovelRepository _novelRepo;
    private readonly INovelChunkRepository _chunkRepo;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly MuseSpaceDbContext _db;
    private readonly IImportProgressNotifier _notifier;
    private readonly ILogger<EmbedNovelJob> _logger;

    public EmbedNovelJob(
        INovelRepository novelRepo,
        INovelChunkRepository chunkRepo,
        IEmbeddingClient embeddingClient,
        MuseSpaceDbContext db,
        IImportProgressNotifier notifier,
        ILogger<EmbedNovelJob> logger)
    {
        _novelRepo = novelRepo;
        _chunkRepo = chunkRepo;
        _embeddingClient = embeddingClient;
        _db = db;
        _notifier = notifier;
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

        try
        {
            var chunks = await _chunkRepo.GetUnembeddedAsync(novelId);
            var total = chunks.Count;
            var done = 0;

            _logger.LogInformation("EmbedNovelJob: {Total} chunks to embed for novel {NovelId}", total, novelId);

            // Process in batches to respect API rate limits
            for (int i = 0; i < chunks.Count; i += BatchSize)
            {
                var batch = chunks.Skip(i).Take(BatchSize).ToList();

                var embeddingTasks = batch.Select(async chunk =>
                {
                    var vector = await _embeddingClient.EmbedAsync(chunk.Content);
                    return (chunk, vector);
                });

                var results = await Task.WhenAll(embeddingTasks);

                // Write embeddings to DB
                foreach (var (chunk, vector) in results)
                {
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
                    await _chunkRepo.MarkEmbeddedAsync(chunk.Id);
                }

                await _db.SaveChangesAsync();
                done += batch.Count;

                // Push progress via SignalR
                await _notifier.NotifyEmbedProgressAsync(novelId, done, total);

                _logger.LogInformation("EmbedNovelJob: {Done}/{Total} chunks embedded for novel {NovelId}",
                    done, total, novelId);
            }

            // Mark novel as fully indexed
            novel.Status = NovelStatus.Indexed;
            novel.UpdatedAt = DateTime.UtcNow;
            await _novelRepo.UpdateAsync(novel);

            await _notifier.NotifyImportDoneAsync(novelId);

            _logger.LogInformation("EmbedNovelJob completed for novel {NovelId}", novelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmbedNovelJob failed for novel {NovelId}", novelId);
            novel.Status = NovelStatus.Failed;
            novel.UpdatedAt = DateTime.UtcNow;
            await _novelRepo.UpdateAsync(novel);
            await _notifier.NotifyImportFailedAsync(novelId, ex.Message);
            throw;
        }
    }
}
