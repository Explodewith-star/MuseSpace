using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfChapterEventRepository : IChapterEventRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfChapterEventRepository(MuseSpaceDbContext db) => _db = db;

    public Task<List<ChapterEvent>> GetByProjectAsync(Guid projectId, CancellationToken ct = default)
        => _db.ChapterEvents.AsNoTracking()
            .Where(e => e.StoryProjectId == projectId)
            .OrderBy(e => e.ChapterId).ThenBy(e => e.Order)
            .ToListAsync(ct);

    public Task<List<ChapterEvent>> GetByOutlineAsync(
        Guid projectId,
        Guid storyOutlineId,
        CancellationToken ct = default)
        => _db.ChapterEvents.AsNoTracking()
            .Where(e => e.StoryProjectId == projectId && e.StoryOutlineId == storyOutlineId)
            .OrderBy(e => e.ChapterId).ThenBy(e => e.Order)
            .ToListAsync(ct);

    public Task<List<ChapterEvent>> GetByChapterAsync(Guid projectId, Guid chapterId, CancellationToken ct = default)
        => _db.ChapterEvents.AsNoTracking()
            .Where(e => e.StoryProjectId == projectId && e.ChapterId == chapterId)
            .OrderBy(e => e.Order)
            .ToListAsync(ct);

    public async Task<List<ChapterEvent>> GetRecentAsync(Guid projectId, int chapterCount, CancellationToken ct = default)
    {
        if (chapterCount <= 0) return new List<ChapterEvent>();
        // 取最近 N 个有事件的章节（按 Chapter.Number 排序），返回这些章节里的全部事件。
        var recentChapterIds = await _db.Chapters.AsNoTracking()
            .Where(c => c.StoryProjectId == projectId)
            .OrderByDescending(c => c.Number)
            .Take(chapterCount)
            .Select(c => c.Id)
            .ToListAsync(ct);
        return await _db.ChapterEvents.AsNoTracking()
            .Where(e => e.StoryProjectId == projectId && recentChapterIds.Contains(e.ChapterId))
            .OrderBy(e => e.ChapterId).ThenBy(e => e.Order)
            .ToListAsync(ct);
    }

    public Task<List<ChapterEvent>> GetIrreversibleAsync(Guid projectId, CancellationToken ct = default)
        => _db.ChapterEvents.AsNoTracking()
            .Where(e => e.StoryProjectId == projectId && e.IsIrreversible)
            .OrderBy(e => e.ChapterId).ThenBy(e => e.Order)
            .ToListAsync(ct);

    public Task<List<ChapterEvent>> GetIrreversibleByOutlineAsync(
        Guid projectId,
        Guid storyOutlineId,
        CancellationToken ct = default)
        => _db.ChapterEvents.AsNoTracking()
            .Where(e => e.StoryProjectId == projectId
                && e.StoryOutlineId == storyOutlineId
                && e.IsIrreversible)
            .OrderBy(e => e.ChapterId).ThenBy(e => e.Order)
            .ToListAsync(ct);

    public Task<ChapterEvent?> GetByIdAsync(Guid projectId, Guid id, CancellationToken ct = default)
        => _db.ChapterEvents.FirstOrDefaultAsync(e => e.StoryProjectId == projectId && e.Id == id, ct);

    public async Task<ChapterEvent> AddAsync(ChapterEvent ev, CancellationToken ct = default)
    {
        if (ev.StoryOutlineId == Guid.Empty)
        {
            ev.StoryOutlineId = await _db.Chapters.AsNoTracking()
                .Where(c => c.StoryProjectId == ev.StoryProjectId && c.Id == ev.ChapterId)
                .Select(c => c.StoryOutlineId)
                .FirstAsync(ct);
        }
        ev.CreatedAt = ev.UpdatedAt = DateTime.UtcNow;
        _db.ChapterEvents.Add(ev);
        await _db.SaveChangesAsync(ct);
        return ev;
    }

    public async Task UpdateAsync(ChapterEvent ev, CancellationToken ct = default)
    {
        if (ev.StoryOutlineId == Guid.Empty)
        {
            ev.StoryOutlineId = await _db.Chapters.AsNoTracking()
                .Where(c => c.StoryProjectId == ev.StoryProjectId && c.Id == ev.ChapterId)
                .Select(c => c.StoryOutlineId)
                .FirstAsync(ct);
        }
        ev.UpdatedAt = DateTime.UtcNow;
        _db.ChapterEvents.Update(ev);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid projectId, Guid id, CancellationToken ct = default)
    {
        var item = await _db.ChapterEvents.FirstOrDefaultAsync(
            e => e.StoryProjectId == projectId && e.Id == id, ct);
        if (item is null) return;
        _db.ChapterEvents.Remove(item);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReplaceForChapterAsync(Guid projectId, Guid chapterId, IReadOnlyList<ChapterEvent> events, CancellationToken ct = default)
    {
        var existing = await _db.ChapterEvents
            .Where(e => e.StoryProjectId == projectId && e.ChapterId == chapterId)
            .ToListAsync(ct);
        if (existing.Count > 0) _db.ChapterEvents.RemoveRange(existing);

        var now = DateTime.UtcNow;
        foreach (var ev in events)
        {
            ev.Id = ev.Id == Guid.Empty ? Guid.NewGuid() : ev.Id;
            ev.StoryProjectId = projectId;
            ev.ChapterId = chapterId;
            if (ev.StoryOutlineId == Guid.Empty)
            {
                ev.StoryOutlineId = await _db.Chapters.AsNoTracking()
                    .Where(c => c.StoryProjectId == projectId && c.Id == chapterId)
                    .Select(c => c.StoryOutlineId)
                    .FirstAsync(ct);
            }
            ev.CreatedAt = ev.UpdatedAt = now;
            _db.ChapterEvents.Add(ev);
        }
        await _db.SaveChangesAsync(ct);
    }
}
