using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface ICharacterRepository
{
    Task<List<Character>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Character?> GetByIdAsync(Guid projectId, Guid characterId, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid projectId, Character character, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid projectId, Guid characterId, CancellationToken cancellationToken = default);
}
