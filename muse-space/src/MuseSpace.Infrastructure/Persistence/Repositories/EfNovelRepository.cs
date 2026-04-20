using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfNovelRepository : INovelRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfNovelRepository(MuseSpaceDbContext db) => _db = db;

    public async Task<Novel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.Novels.FindAsync([id], cancellationToken);

    public async Task<List<Novel>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await _db.Novels
                    .Where(n => n.StoryProjectId == projectId)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync(cancellationToken);

    public async Task AddAsync(Novel novel, CancellationToken cancellationToken = default)
    {
        _db.Novels.Add(novel);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Novel novel, CancellationToken cancellationToken = default)
    {
        _db.Novels.Update(novel);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.Novels.Where(n => n.Id == id).ExecuteDeleteAsync(cancellationToken);
}
