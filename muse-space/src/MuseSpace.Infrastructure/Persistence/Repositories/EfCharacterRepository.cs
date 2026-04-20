using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfCharacterRepository : ICharacterRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfCharacterRepository(MuseSpaceDbContext db) => _db = db;

    public async Task<List<Character>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await _db.Characters
                    .Where(c => c.StoryProjectId == projectId)
                    .ToListAsync(cancellationToken);

    public async Task<Character?> GetByIdAsync(Guid projectId, Guid characterId, CancellationToken cancellationToken = default)
        => await _db.Characters
                    .FirstOrDefaultAsync(c => c.Id == characterId && c.StoryProjectId == projectId, cancellationToken);

    public async Task SaveAsync(Guid projectId, Character character, CancellationToken cancellationToken = default)
    {
        character.StoryProjectId = projectId;
        var entry = _db.Entry(character);
        entry.State = await _db.Characters.AnyAsync(c => c.Id == character.Id, cancellationToken)
            ? EntityState.Modified
            : EntityState.Added;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid projectId, Guid characterId, CancellationToken cancellationToken = default)
        => await _db.Characters
                    .Where(c => c.Id == characterId && c.StoryProjectId == projectId)
                    .ExecuteDeleteAsync(cancellationToken);
}
