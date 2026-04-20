using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfStoryProjectRepository : IStoryProjectRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfStoryProjectRepository(MuseSpaceDbContext db) => _db = db;

    public async Task<StoryProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.StoryProjects.FindAsync([id], cancellationToken);

    public async Task<List<StoryProject>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _db.StoryProjects
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync(cancellationToken);

    public async Task SaveAsync(StoryProject project, CancellationToken cancellationToken = default)
    {
        var entry = _db.Entry(project);
        entry.State = await _db.StoryProjects.AnyAsync(p => p.Id == project.Id, cancellationToken)
            ? EntityState.Modified
            : EntityState.Added;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.StoryProjects.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken);
}
