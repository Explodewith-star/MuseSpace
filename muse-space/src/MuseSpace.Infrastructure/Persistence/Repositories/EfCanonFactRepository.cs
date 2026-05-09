using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

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

    public Task<List<CanonFact>> GetByOutlineAsync(
        Guid projectId,
        Guid storyOutlineId,
        CancellationToken ct = default)
        => _db.CanonFacts.AsNoTracking()
            .Where(f => f.StoryProjectId == projectId && f.StoryOutlineId == storyOutlineId)
            .OrderByDescending(f => f.UpdatedAt)
            .ToListAsync(ct);

    public Task<List<CanonFact>> GetActiveAsync(Guid projectId, CancellationToken ct = default)
        => _db.CanonFacts.AsNoTracking()
            .Where(f => f.StoryProjectId == projectId && f.InvalidatedByChapterId == null)
            .OrderByDescending(f => f.IsLocked)
            .ThenByDescending(f => f.UpdatedAt)
            .ToListAsync(ct);

    public Task<List<CanonFact>> GetActiveByOutlineAsync(
        Guid projectId,
        Guid storyOutlineId,
        CancellationToken ct = default)
        => _db.CanonFacts.AsNoTracking()
            .Where(f => f.StoryProjectId == projectId
                && f.StoryOutlineId == storyOutlineId
                && f.InvalidatedByChapterId == null)
            .OrderByDescending(f => f.IsLocked)
            .ThenByDescending(f => f.UpdatedAt)
            .ToListAsync(ct);

    public Task<List<CanonFact>> GetLockedAsync(Guid projectId, CancellationToken ct = default)
        => _db.CanonFacts.AsNoTracking()
            .Where(f => f.StoryProjectId == projectId && f.IsLocked && f.InvalidatedByChapterId == null)
            .OrderByDescending(f => f.UpdatedAt)
            .ToListAsync(ct);

    public Task<List<CanonFact>> GetLockedByOutlineAsync(
        Guid projectId,
        Guid storyOutlineId,
        CancellationToken ct = default)
        => _db.CanonFacts.AsNoTracking()
            .Where(f => f.StoryProjectId == projectId
                && f.StoryOutlineId == storyOutlineId
                && f.IsLocked
                && f.InvalidatedByChapterId == null)
            .OrderByDescending(f => f.UpdatedAt)
            .ToListAsync(ct);

    public Task<CanonFact?> GetByIdAsync(Guid projectId, Guid id, CancellationToken ct = default)
        => _db.CanonFacts.FirstOrDefaultAsync(f => f.StoryProjectId == projectId && f.Id == id, ct);

    public Task<CanonFact?> GetByKeyAsync(Guid projectId, string factType, string factKey, CancellationToken ct = default)
        => _db.CanonFacts.FirstOrDefaultAsync(
            f => f.StoryProjectId == projectId && f.FactType == factType && f.FactKey == factKey, ct);

    public Task<CanonFact?> GetByKeyAsync(
        Guid projectId,
        Guid storyOutlineId,
        string factType,
        string factKey,
        CancellationToken ct = default)
        => _db.CanonFacts.FirstOrDefaultAsync(
            f => f.StoryProjectId == projectId
                && f.StoryOutlineId == storyOutlineId
                && f.FactType == factType
                && f.FactKey == factKey, ct);

    public async Task<CanonFact> AddAsync(CanonFact fact, CancellationToken ct = default)
    {
        await EnsureStoryOutlineAsync(fact, ct);
        fact.CreatedAt = fact.UpdatedAt = DateTime.UtcNow;
        _db.CanonFacts.Add(fact);
        await _db.SaveChangesAsync(ct);
        return fact;
    }

    public async Task UpdateAsync(CanonFact fact, CancellationToken ct = default)
    {
        await EnsureStoryOutlineAsync(fact, ct);
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

    private async Task EnsureStoryOutlineAsync(CanonFact fact, CancellationToken ct)
    {
        if (fact.SourceChapterId.HasValue)
        {
            var sourceOutlineId = await _db.Chapters.AsNoTracking()
                .Where(c => c.StoryProjectId == fact.StoryProjectId && c.Id == fact.SourceChapterId.Value)
                .Select(c => (Guid?)c.StoryOutlineId)
                .FirstOrDefaultAsync(ct);
            if (sourceOutlineId.HasValue)
            {
                fact.StoryOutlineId = sourceOutlineId.Value;
                return;
            }
        }

        if (fact.StoryOutlineId != Guid.Empty) return;

        var existing = await _db.StoryOutlines
            .Where(o => o.StoryProjectId == fact.StoryProjectId && o.IsDefault)
            .OrderBy(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            fact.StoryOutlineId = existing.Id;
            return;
        }

        var outline = new StoryOutline
        {
            Id = Guid.NewGuid(),
            StoryProjectId = fact.StoryProjectId,
            Name = "原创主线",
            Mode = GenerationMode.Original,
            DivergencePolicy = DivergencePolicy.SoftCanon,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.StoryOutlines.Add(outline);
        await _db.SaveChangesAsync(ct);
        fact.StoryOutlineId = outline.Id;
    }
}
