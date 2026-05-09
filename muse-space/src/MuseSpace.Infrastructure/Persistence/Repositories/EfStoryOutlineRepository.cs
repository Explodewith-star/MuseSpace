using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfStoryOutlineRepository : IStoryOutlineRepository
{
    private readonly MuseSpaceDbContext _db;

    public EfStoryOutlineRepository(MuseSpaceDbContext db) => _db = db;

    public Task<List<StoryOutline>> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
        => _db.StoryOutlines
            .Where(o => o.StoryProjectId == projectId)
            .OrderByDescending(o => o.IsDefault)
            .ThenBy(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<StoryOutline?> GetByIdAsync(
        Guid projectId,
        Guid outlineId,
        CancellationToken cancellationToken = default)
        => _db.StoryOutlines
            .FirstOrDefaultAsync(o => o.Id == outlineId && o.StoryProjectId == projectId, cancellationToken);

    public async Task<StoryOutline> GetOrCreateDefaultAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.StoryOutlines
            .Where(o => o.StoryProjectId == projectId && o.IsDefault)
            .OrderBy(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null) return existing;

        var outline = new StoryOutline
        {
            Id = Guid.NewGuid(),
            StoryProjectId = projectId,
            Name = "原创主线",
            Mode = GenerationMode.Original,
            DivergencePolicy = DivergencePolicy.SoftCanon,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.StoryOutlines.Add(outline);
        await _db.SaveChangesAsync(cancellationToken);
        return outline;
    }

    public async Task SaveAsync(
        Guid projectId,
        StoryOutline outline,
        CancellationToken cancellationToken = default)
    {
        outline.StoryProjectId = projectId;
        outline.UpdatedAt = DateTime.UtcNow;

        if (outline.IsDefault)
        {
            var otherDefaults = await _db.StoryOutlines
                .Where(o => o.StoryProjectId == projectId && o.Id != outline.Id && o.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var other in otherDefaults)
                other.IsDefault = false;
        }

        var entry = _db.Entry(outline);
        entry.State = await _db.StoryOutlines.AnyAsync(o => o.Id == outline.Id, cancellationToken)
            ? EntityState.Modified
            : EntityState.Added;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(
        Guid projectId,
        Guid outlineId,
        CancellationToken cancellationToken = default)
        => _db.StoryOutlines
            .Where(o => o.Id == outlineId && o.StoryProjectId == projectId && !o.IsDefault)
            .ExecuteDeleteAsync(cancellationToken);
}
