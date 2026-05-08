using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfChapterBatchDraftRunRepository : IChapterBatchDraftRunRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfChapterBatchDraftRunRepository(MuseSpaceDbContext db) => _db = db;

    public async Task<ChapterBatchDraftRun> AddAsync(ChapterBatchDraftRun run, CancellationToken ct = default)
    {
        _db.ChapterBatchDraftRuns.Add(run);
        await _db.SaveChangesAsync(ct);
        return run;
    }

    public async Task UpdateAsync(ChapterBatchDraftRun run, CancellationToken ct = default)
    {
        _db.ChapterBatchDraftRuns.Update(run);
        await _db.SaveChangesAsync(ct);
    }

    public Task<ChapterBatchDraftRun?> GetAsync(Guid projectId, Guid runId, CancellationToken ct = default)
        => _db.ChapterBatchDraftRuns.AsNoTracking()
              .FirstOrDefaultAsync(r => r.Id == runId && r.StoryProjectId == projectId, ct);

    // 单批硬超时 45 分钟，这里用 60 分钟作为过期判断缓冲。
    // 超时的 Pending/Running run 被视为死锁，不再阻塞新任务提交。
    private static readonly TimeSpan StaleRunThreshold = TimeSpan.FromMinutes(60);

    public Task<bool> HasActiveAsync(Guid projectId, CancellationToken ct = default)
    {
        var expiryCutoff = DateTime.UtcNow - StaleRunThreshold;
        return _db.ChapterBatchDraftRuns.AnyAsync(r =>
            r.StoryProjectId == projectId &&
            (r.Status == ChapterBatchDraftStatus.Pending || r.Status == ChapterBatchDraftStatus.Running) &&
            r.CreatedAt >= expiryCutoff,
            ct);
    }

    public async Task MarkStaleRunsAsFailedAsync(Guid projectId, CancellationToken ct = default)
    {
        var expiryCutoff = DateTime.UtcNow - StaleRunThreshold;
        var stale = await _db.ChapterBatchDraftRuns
            .Where(r => r.StoryProjectId == projectId &&
                        (r.Status == ChapterBatchDraftStatus.Pending || r.Status == ChapterBatchDraftStatus.Running) &&
                        r.CreatedAt < expiryCutoff)
            .ToListAsync(ct);
        foreach (var r in stale)
        {
            r.Status = ChapterBatchDraftStatus.Failed;
            r.ErrorMessage = "任务卡死（超过60分钟未完成），已自动清除";
            r.FinishedAt ??= DateTime.UtcNow;
        }
        if (stale.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    public async Task<List<ChapterBatchDraftRun>> ListRecentAsync(
        Guid projectId, int take = 10, CancellationToken ct = default)
        => await _db.ChapterBatchDraftRuns.AsNoTracking()
            .Where(r => r.StoryProjectId == projectId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
}
