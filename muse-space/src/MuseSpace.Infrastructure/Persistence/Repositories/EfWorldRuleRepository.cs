using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfWorldRuleRepository : IWorldRuleRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfWorldRuleRepository(MuseSpaceDbContext db) => _db = db;

    public async Task<List<WorldRule>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await _db.WorldRules
                    .Where(w => w.StoryProjectId == projectId)
                    .OrderByDescending(w => w.Priority)
                    .ToListAsync(cancellationToken);

    public async Task<WorldRule?> GetByIdAsync(Guid projectId, Guid ruleId, CancellationToken cancellationToken = default)
        => await _db.WorldRules
                    .FirstOrDefaultAsync(w => w.Id == ruleId && w.StoryProjectId == projectId, cancellationToken);

    public async Task SaveAsync(Guid projectId, WorldRule rule, CancellationToken cancellationToken = default)
    {
        rule.StoryProjectId = projectId;
        var entry = _db.Entry(rule);
        entry.State = await _db.WorldRules.AnyAsync(w => w.Id == rule.Id, cancellationToken)
            ? EntityState.Modified
            : EntityState.Added;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid projectId, Guid ruleId, CancellationToken cancellationToken = default)
        => await _db.WorldRules
                    .Where(w => w.Id == ruleId && w.StoryProjectId == projectId)
                    .ExecuteDeleteAsync(cancellationToken);
}
