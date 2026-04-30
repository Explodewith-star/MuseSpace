using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IChapterBatchDraftRunRepository
{
    Task<ChapterBatchDraftRun> AddAsync(ChapterBatchDraftRun run, CancellationToken ct = default);
    Task UpdateAsync(ChapterBatchDraftRun run, CancellationToken ct = default);
    Task<ChapterBatchDraftRun?> GetAsync(Guid projectId, Guid runId, CancellationToken ct = default);

    /// <summary>项目级是否已有 Pending/Running 批次。</summary>
    Task<bool> HasActiveAsync(Guid projectId, CancellationToken ct = default);

    Task<List<ChapterBatchDraftRun>> ListRecentAsync(Guid projectId, int take = 10, CancellationToken ct = default);
}
