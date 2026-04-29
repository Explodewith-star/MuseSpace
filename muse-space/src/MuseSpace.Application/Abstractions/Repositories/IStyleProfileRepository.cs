using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IStyleProfileRepository
{
    Task<StyleProfile?> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid projectId, StyleProfile profile, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid projectId, Guid profileId, CancellationToken cancellationToken = default);
}
