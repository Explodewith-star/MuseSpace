using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IStoryOutlineRepository
{
    Task<List<StoryOutline>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<StoryOutline?> GetByIdAsync(Guid projectId, Guid outlineId, CancellationToken cancellationToken = default);
    Task<StoryOutline> GetOrCreateDefaultAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid projectId, StoryOutline outline, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid projectId, Guid outlineId, CancellationToken cancellationToken = default);
}
