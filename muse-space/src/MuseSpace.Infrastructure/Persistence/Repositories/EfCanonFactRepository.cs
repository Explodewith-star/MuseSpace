using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfCanonFactRepository : ICanonFactRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfCanonFactRepository(MuseSpaceDbContext db) => _db = db;

    public Task<List<CanonFact>> GetByProjectAsync(Guid projectId, CancellationToken ct = default)
        => _db.CanonFacts.AsNoTracking()
            .Where(f => f.StoryProjectId == projectId)
            .OrderByDescending(f => f.UpdatedAt)
            .ToListAsync(ct);

    public Task<List<CanonFact>> GetActiveAsync(Guid projectId, CancellationToken ct = default)
        => _db.CanonFacts.AsNoTracking()
            .Where(f => f.StoryProjectId == projectId && f.InvalidatedByChapterId == null)
            .OrderByDescending(f => f.IsLocked)
            .ThenByDescending(f => f.UpdatedAt)
            .ToListAsync(ct);

    public Task<List<CanonFact>> GetLockedAsync(Guid projectId, CancellationToken ct = default)
        => _db.CanonFacts.AsNoTracking()
            .Where(f => f.StoryProjectId == projectId && f.IsLocked && f.InvalidatedByChapterId == null)
            .OrderByDescending(f => f.UpdatedAt)
            .ToListAsync(ct);

    public Task<CanonFact?> GetByIdAsync(Guid projectId, Guid id, CancellationToken ct = default)
        => _db.CanonFacts.FirstOrDefaultAsync(f => f.StoryProjectId == projectId && f.Id == id, ct);

    public Task<CanonFact?> GetByKeyAsync(Guid projectId, string factType, string factKey, CancellationToken ct = default)
        => _db.CanonFacts.FirstOrDefaultAsync(
            f => f.StoryProjectId == projectId && f.FactType == factType && f.FactKey == factKey, ct);

    public async Task<CanonFact> AddAsync(CanonFact fact, CancellationToken ct = default)
    {
        fact.CreatedAt = fact.UpdatedAt = DateTime.UtcNow;
        _db.CanonFacts.Add(fact);
        await _db.SaveChangesAsync(ct);
        return fact;
    }

    public async Task UpdateAsync(CanonFact fact, CancellationToken ct = default)
    {
        fact.UpdatedAt = DateTime.UtcNow;
        _db.CanonFacts.Update(fact);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid projectId, Guid id, CancellationToken ct = default)
    {
        var item = await _db.CanonFacts.FirstOrDefaultAsync(f => f.StoryProjectId == projectId && f.Id == id, ct);
        if (item is null) return;
        _db.CanonFacts.Remove(item);
        await _db.SaveChangesAsync(ct);
    }
}
