using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface INovelCharacterSnapshotRepository
{
    Task<List<NovelCharacterSnapshot>> GetByNovelAsync(Guid novelId, CancellationToken cancellationToken = default);
    Task<List<NovelCharacterSnapshot>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<NovelCharacterSnapshot> snapshots, CancellationToken cancellationToken = default);
    Task DeleteByNovelAsync(Guid novelId, CancellationToken cancellationToken = default);
}
