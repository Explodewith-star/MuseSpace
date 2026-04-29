using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IAgentSuggestionRepository
{
    Task<AgentSuggestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<AgentSuggestion>> GetByProjectAsync(Guid projectId, string? category = null, SuggestionStatus? status = null, Guid? targetEntityId = null, CancellationToken cancellationToken = default);
    Task<List<AgentSuggestion>> GetByAgentRunAsync(Guid agentRunId, CancellationToken cancellationToken = default);
    Task<List<AgentSuggestion>> GetByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default);
    Task AddAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default);
    Task UpdateAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> DeleteByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default);
    /// <summary>
    /// 删除来源为指定原著、且状态不是 Applied 的所有建议（物理删除）。
    /// Applied 的建议已转为正式资产，不受原著删除影响。
    /// </summary>
    Task<int> DeleteBySourceNovelIdAsync(Guid novelId, CancellationToken cancellationToken = default);
}
