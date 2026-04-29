using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfStyleProfileRepository : IStyleProfileRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfStyleProfileRepository(MuseSpaceDbContext db) => _db = db;

    public async Task<StyleProfile?> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await _db.StyleProfiles
                    .FirstOrDefaultAsync(s => s.StoryProjectId == projectId, cancellationToken);

    public async Task SaveAsync(Guid projectId, StyleProfile profile, CancellationToken cancellationToken = default)
    {
        profile.StoryProjectId = projectId;
        var entry = _db.Entry(profile);
        entry.State = await _db.StyleProfiles.AnyAsync(s => s.Id == profile.Id, cancellationToken)
            ? EntityState.Modified
            : EntityState.Added;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid projectId, Guid profileId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.StyleProfiles
            .FirstOrDefaultAsync(s => s.StoryProjectId == projectId && s.Id == profileId, cancellationToken);
        if (profile is not null)
        {
            _db.StyleProfiles.Remove(profile);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
