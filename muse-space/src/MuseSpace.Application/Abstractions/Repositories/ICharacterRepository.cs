using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface ICharacterRepository
{
    Task<List<Character>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<List<Character>> GetByOutlineAsync(Guid outlineId, CancellationToken cancellationToken = default);
    /// <summary>获取项目的原著角色池（StoryOutlineId IS NULL）。</summary>
    Task<List<Character>> GetPoolByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Character?> GetByIdAsync(Guid projectId, Guid characterId, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid projectId, Character character, CancellationToken cancellationToken = default);
    Task SaveManyAsync(Guid projectId, IEnumerable<Character> characters, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid projectId, Guid characterId, CancellationToken cancellationToken = default);
}
