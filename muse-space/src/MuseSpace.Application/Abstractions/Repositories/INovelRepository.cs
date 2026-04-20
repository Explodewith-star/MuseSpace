using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface INovelRepository
{
    Task<Novel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Novel>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task AddAsync(Novel novel, CancellationToken cancellationToken = default);
    Task UpdateAsync(Novel novel, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
