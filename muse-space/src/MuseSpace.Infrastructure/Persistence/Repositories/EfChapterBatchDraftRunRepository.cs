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

    public Task<bool> HasActiveAsync(Guid projectId, CancellationToken ct = default)
        => _db.ChapterBatchDraftRuns.AnyAsync(r =>
            r.StoryProjectId == projectId &&
            (r.Status == ChapterBatchDraftStatus.Pending || r.Status == ChapterBatchDraftStatus.Running),
            ct);

    public async Task<List<ChapterBatchDraftRun>> ListRecentAsync(
        Guid projectId, int take = 10, CancellationToken ct = default)
        => await _db.ChapterBatchDraftRuns.AsNoTracking()
            .Where(r => r.StoryProjectId == projectId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
}
