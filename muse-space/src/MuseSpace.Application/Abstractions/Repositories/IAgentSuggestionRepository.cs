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

    /// <summary>
    /// 删除 TargetEntityId 匹配指定实体（如大纲 ID）的所有建议（物理删除）。
    /// 用于大纲删除时清理关联的大纲规划建议，防止孤儿数据残留。
    /// </summary>
    Task<int> DeleteByTargetEntityIdAsync(Guid targetEntityId, CancellationToken cancellationToken = default);
}
