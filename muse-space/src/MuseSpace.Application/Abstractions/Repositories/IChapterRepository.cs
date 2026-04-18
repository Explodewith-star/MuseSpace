using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IChapterRepository
{
    Task<List<Chapter>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Chapter?> GetByIdAsync(Guid projectId, Guid chapterId, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid projectId, Chapter chapter, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid projectId, Guid chapterId, CancellationToken cancellationToken = default);
}
