using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfChapterRepository : IChapterRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfChapterRepository(MuseSpaceDbContext db) => _db = db;

    public async Task<List<Chapter>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await _db.Chapters
                    .Where(c => c.StoryProjectId == projectId)
                    .OrderBy(c => c.Number)
                    .ToListAsync(cancellationToken);

    public async Task<Chapter?> GetByIdAsync(Guid projectId, Guid chapterId, CancellationToken cancellationToken = default)
        => await _db.Chapters
                    .FirstOrDefaultAsync(c => c.Id == chapterId && c.StoryProjectId == projectId, cancellationToken);

    public async Task SaveAsync(Guid projectId, Chapter chapter, CancellationToken cancellationToken = default)
    {
        chapter.StoryProjectId = projectId;
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
}
