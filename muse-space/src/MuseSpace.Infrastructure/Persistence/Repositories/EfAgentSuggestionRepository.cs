using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfAgentSuggestionRepository : IAgentSuggestionRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfAgentSuggestionRepository(MuseSpaceDbContext db) => _db = db;

    public async Task<AgentSuggestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.AgentSuggestions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<List<AgentSuggestion>> GetByProjectAsync(
        Guid projectId, string? category = null, SuggestionStatus? status = null, Guid? targetEntityId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.AgentSuggestions.Where(s => s.StoryProjectId == projectId);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(s => s.Category == category);

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        if (targetEntityId.HasValue)
            query = query.Where(s => s.TargetEntityId == targetEntityId.Value);

        return await query.OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<List<AgentSuggestion>> GetByAgentRunAsync(Guid agentRunId, CancellationToken cancellationToken = default)
        => await _db.AgentSuggestions
                    .Where(s => s.AgentRunId == agentRunId)
                    .OrderByDescending(s => s.CreatedAt)
                    .ToListAsync(cancellationToken);

    public async Task<List<AgentSuggestion>> GetByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default)
        => await _db.AgentSuggestions
                    .Where(s => ids.Contains(s.Id))
                    .ToListAsync(cancellationToken);

    public async Task AddAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        _db.AgentSuggestions.Add(suggestion);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        _db.AgentSuggestions.Update(suggestion);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _db.AgentSuggestions
            .Where(s => s.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default)
    {
        return await _db.AgentSuggestions
            .Where(s => ids.Contains(s.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteBySourceNovelIdAsync(Guid novelId, CancellationToken cancellationToken = default)
    {
        return await _db.AgentSuggestions
            .Where(s => s.SourceNovelId == novelId && s.Status != SuggestionStatus.Applied)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
