using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IStoryProjectRepository
{
    Task<StoryProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<StoryProject>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(StoryProject project, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
