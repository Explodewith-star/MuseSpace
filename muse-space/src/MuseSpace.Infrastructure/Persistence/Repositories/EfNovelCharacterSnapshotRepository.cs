using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfNovelCharacterSnapshotRepository : INovelCharacterSnapshotRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfNovelCharacterSnapshotRepository(MuseSpaceDbContext db) => _db = db;

    public Task<List<NovelCharacterSnapshot>> GetByNovelAsync(Guid novelId, CancellationToken cancellationToken = default)
        => _db.NovelCharacterSnapshots.Where(s => s.NovelId == novelId).ToListAsync(cancellationToken);

    public Task<List<NovelCharacterSnapshot>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => _db.NovelCharacterSnapshots.Where(s => s.StoryProjectId == projectId).ToListAsync(cancellationToken);

    public async Task AddRangeAsync(IEnumerable<NovelCharacterSnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        _db.NovelCharacterSnapshots.AddRange(snapshots);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByNovelAsync(Guid novelId, CancellationToken cancellationToken = default)
    {
        await _db.NovelCharacterSnapshots
            .Where(s => s.NovelId == novelId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
