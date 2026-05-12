using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfOutlineChainRepository : IOutlineChainRepository
{
    private readonly MuseSpaceDbContext _db;

    public EfOutlineChainRepository(MuseSpaceDbContext db) => _db = db;

    public Task<List<OutlineChain>> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => _db.OutlineChains
            .Where(c => c.StoryProjectId == projectId)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<OutlineChain?> GetByIdAsync(
        Guid chainId,
        CancellationToken cancellationToken = default)
        => _db.OutlineChains
            .FirstOrDefaultAsync(c => c.Id == chainId, cancellationToken);

    public async Task SaveAsync(
        OutlineChain chain,
        CancellationToken cancellationToken = default)
    {
        var exists = await _db.OutlineChains.AnyAsync(c => c.Id == chain.Id, cancellationToken);
        _db.Entry(chain).State = exists ? EntityState.Modified : EntityState.Added;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(
        Guid chainId,
        CancellationToken cancellationToken = default)
        => _db.OutlineChains
            .Where(c => c.Id == chainId)
            .ExecuteDeleteAsync(cancellationToken);
}
