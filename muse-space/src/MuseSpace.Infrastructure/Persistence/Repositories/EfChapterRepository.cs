using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfChapterRepository : IChapterRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfChapterRepository(MuseSpaceDbContext db) => _db = db;

    public async Task<List<Chapter>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var list = await _db.Chapters
                    .Where(c => c.StoryProjectId == projectId)
                    .OrderBy(c => c.Number)
                    .ToListAsync(cancellationToken);
        foreach (var c in list)
        {
            c.KeyCharacterIds ??= new List<Guid>();
            c.MustIncludePoints ??= new List<string>();
        }
        return list;
    }

    public async Task<Chapter?> GetByIdAsync(Guid projectId, Guid chapterId, CancellationToken cancellationToken = default)
    {
        var chapter = await _db.Chapters
                    .FirstOrDefaultAsync(c => c.Id == chapterId && c.StoryProjectId == projectId, cancellationToken);
        if (chapter is not null)
        {
            chapter.KeyCharacterIds ??= new List<Guid>();
            chapter.MustIncludePoints ??= new List<string>();
        }
        return chapter;
    }

    public async Task SaveAsync(Guid projectId, Chapter chapter, CancellationToken cancellationToken = default)
    {
        chapter.StoryProjectId = projectId;
        // 确保集合字段非 null（兼容迁移前 DB 旧行）
        chapter.KeyCharacterIds ??= new List<Guid>();
        chapter.MustIncludePoints ??= new List<string>();
        var entry = _db.Entry(chapter);
        entry.State = await _db.Chapters.AnyAsync(c => c.Id == chapter.Id, cancellationToken)
            ? EntityState.Modified
            : EntityState.Added;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid projectId, Guid chapterId, CancellationToken cancellationToken = default)
        => await _db.Chapters
                    .Where(c => c.Id == chapterId && c.StoryProjectId == projectId)
                    .ExecuteDeleteAsync(cancellationToken);

    public async Task<int> BatchDeleteAsync(Guid projectId, IEnumerable<Guid> chapterIds, CancellationToken cancellationToken = default)
    {
        var ids = chapterIds.ToList();
        if (ids.Count == 0) return 0;
        // Scene 有级联 FK，删 Chapter 时 Scene 自动被 DB 级联删除
        return await _db.Chapters
                    .Where(c => c.StoryProjectId == projectId && ids.Contains(c.Id))
                    .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteBySourceSuggestionIdAsync(Guid suggestionId, CancellationToken cancellationToken = default)
        => await _db.Chapters
                    .Where(c => c.SourceSuggestionId == suggestionId)
                    .ExecuteDeleteAsync(cancellationToken);
}
