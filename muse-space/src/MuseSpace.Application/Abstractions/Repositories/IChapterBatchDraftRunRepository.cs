using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IChapterBatchDraftRunRepository
{
    Task<ChapterBatchDraftRun> AddAsync(ChapterBatchDraftRun run, CancellationToken ct = default);
    Task UpdateAsync(ChapterBatchDraftRun run, CancellationToken ct = default);
    Task<ChapterBatchDraftRun?> GetAsync(Guid projectId, Guid runId, CancellationToken ct = default);

    /// <summary>项目级是否已有 Pending/Running 批次。</summary>
    Task<bool> HasActiveAsync(Guid projectId, CancellationToken ct = default);
    /// <summary>指定大纲是否已有 Pending/Running 批次。</summary>
    Task<bool> HasActiveAsync(Guid projectId, Guid storyOutlineId, CancellationToken ct = default);

    /// <summary>将超过超时閘值仍处于 Pending/Running 的历史批次标记为 Failed（防止死锁）。</summary>
    Task MarkStaleRunsAsFailedAsync(Guid projectId, CancellationToken ct = default);

    Task<List<ChapterBatchDraftRun>> ListRecentAsync(Guid projectId, int take = 10, CancellationToken ct = default);
    Task<List<ChapterBatchDraftRun>> ListRecentAsync(Guid projectId, Guid storyOutlineId, int take = 10, CancellationToken ct = default);
}
