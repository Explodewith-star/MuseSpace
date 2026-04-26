using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfNovelChunkRepository : INovelChunkRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfNovelChunkRepository(MuseSpaceDbContext db) => _db = db;

    public async Task<List<NovelChunk>> GetByNovelAsync(Guid novelId, CancellationToken cancellationToken = default)
        => await _db.NovelChunks
                    .Where(c => c.NovelId == novelId)
                    .OrderBy(c => c.ChunkIndex)
                    .ToListAsync(cancellationToken);

    public async Task<List<NovelChunk>> GetUnembeddedAsync(Guid novelId, CancellationToken cancellationToken = default)
        => await _db.NovelChunks
                    .Where(c => c.NovelId == novelId && !c.IsEmbedded)
                    .OrderBy(c => c.ChunkIndex)
                    .ToListAsync(cancellationToken);

    public async Task AddRangeAsync(IEnumerable<NovelChunk> chunks, CancellationToken cancellationToken = default)
    {
        await _db.NovelChunks.AddRangeAsync(chunks, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkEmbeddedAsync(Guid chunkId, CancellationToken cancellationToken = default)
        => await _db.NovelChunks
                    .Where(c => c.Id == chunkId)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsEmbedded, true), cancellationToken);

    public async Task MarkEmbeddedBatchAsync(IEnumerable<Guid> chunkIds, CancellationToken cancellationToken = default)
    {
        var ids = chunkIds.Distinct().ToArray();
        if (ids.Length == 0)
            return;

        await _db.NovelChunks
            .Where(c => ids.Contains(c.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsEmbedded, true), cancellationToken);
    }

    public async Task DeleteByNovelAsync(Guid novelId, CancellationToken cancellationToken = default)
        => await _db.NovelChunks.Where(c => c.NovelId == novelId).ExecuteDeleteAsync(cancellationToken);
}
