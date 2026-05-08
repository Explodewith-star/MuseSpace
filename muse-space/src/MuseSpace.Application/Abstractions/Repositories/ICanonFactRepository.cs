using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface ICanonFactRepository
{
    Task<List<CanonFact>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<List<CanonFact>> GetActiveAsync(Guid projectId, CancellationToken ct = default);
    Task<List<CanonFact>> GetLockedAsync(Guid projectId, CancellationToken ct = default);
    Task<CanonFact?> GetByIdAsync(Guid projectId, Guid id, CancellationToken ct = default);
    Task<CanonFact?> GetByKeyAsync(Guid projectId, string factType, string factKey, CancellationToken ct = default);
    Task<CanonFact> AddAsync(CanonFact fact, CancellationToken ct = default);
    Task UpdateAsync(CanonFact fact, CancellationToken ct = default);
    Task DeleteAsync(Guid projectId, Guid id, CancellationToken ct = default);
}
