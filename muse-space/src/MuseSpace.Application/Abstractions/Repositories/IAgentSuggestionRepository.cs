using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IAgentSuggestionRepository
{
    Task<AgentSuggestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<AgentSuggestion>> GetByProjectAsync(Guid projectId, string? category = null, SuggestionStatus? status = null, CancellationToken cancellationToken = default);
    Task<List<AgentSuggestion>> GetByAgentRunAsync(Guid agentRunId, CancellationToken cancellationToken = default);
    Task<List<AgentSuggestion>> GetByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default);
    Task AddAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default);
    Task UpdateAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default);
}
