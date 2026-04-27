using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Abstractions.Suggestions;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

namespace MuseSpace.Application.Services.Suggestions;

public sealed class AgentSuggestionAppService
{
    private readonly IAgentSuggestionRepository _repository;
    private readonly Dictionary<string, ISuggestionApplier> _appliers;

    public AgentSuggestionAppService(
        IAgentSuggestionRepository repository,
        IEnumerable<ISuggestionApplier> appliers)
    {
        _repository = repository;
        _appliers = appliers.ToDictionary(a => a.Category, StringComparer.OrdinalIgnoreCase);
    }

    // ── 创建建议 ──────────────────────────────────────────────────────────────

    public async Task<AgentSuggestionResponse> CreateAsync(
        Guid agentRunId,
        Guid storyProjectId,
        string category,
        string title,
        string contentJson,
        Guid? targetEntityId = null,
        CancellationToken cancellationToken = default)
    {
        var suggestion = new AgentSuggestion
        {
            AgentRunId = agentRunId,
            StoryProjectId = storyProjectId,
            Category = category,
            Title = title,
            ContentJson = contentJson,
            TargetEntityId = targetEntityId,
        };

        await _repository.AddAsync(suggestion, cancellationToken);
        return ToResponse(suggestion);
    }

    // ── 查询 ──────────────────────────────────────────────────────────────────

    public async Task<List<AgentSuggestionResponse>> GetByProjectAsync(
        Guid projectId, string? category = null, SuggestionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var list = await _repository.GetByProjectAsync(projectId, category, status, cancellationToken);
        return list.Select(ToResponse).ToList();
    }

    public async Task<AgentSuggestionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetByIdAsync(id, cancellationToken);
        return s is null ? null : ToResponse(s);
    }

    public async Task<List<AgentSuggestionResponse>> GetByAgentRunAsync(Guid agentRunId, CancellationToken cancellationToken = default)
    {
        var list = await _repository.GetByAgentRunAsync(agentRunId, cancellationToken);
        return list.Select(ToResponse).ToList();
    }

    // ── 状态流转 ──────────────────────────────────────────────────────────────

    public async Task<AgentSuggestionResponse?> AcceptAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetByIdAsync(id, cancellationToken);
        if (s is null || s.Status != SuggestionStatus.Pending) return null;

        s.Status = SuggestionStatus.Accepted;
        s.ResolvedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(s, cancellationToken);
        return ToResponse(s);
    }

    public async Task<AgentSuggestionResponse?> IgnoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetByIdAsync(id, cancellationToken);
        if (s is null || s.Status != SuggestionStatus.Pending) return null;

        s.Status = SuggestionStatus.Ignored;
        s.ResolvedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(s, cancellationToken);
        return ToResponse(s);
    }

    /// <summary>将已接受的建议写入正式业务表。</summary>
    public async Task<AgentSuggestionResponse?> ApplyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetByIdAsync(id, cancellationToken);
        if (s is null || s.Status != SuggestionStatus.Accepted) return null;

        if (!_appliers.TryGetValue(s.Category, out var applier))
            throw new InvalidOperationException($"未找到 Category='{s.Category}' 对应的 Applier");

        var entityId = await applier.ApplyAsync(s, cancellationToken);

        s.Status = SuggestionStatus.Applied;
        s.TargetEntityId = entityId;
        await _repository.UpdateAsync(s, cancellationToken);
        return ToResponse(s);
    }

    /// <summary>批量接受或忽略。</summary>
    public async Task<int> BatchResolveAsync(List<Guid> ids, string action, CancellationToken cancellationToken = default)
    {
        var targetStatus = action.Equals("Accept", StringComparison.OrdinalIgnoreCase)
            ? SuggestionStatus.Accepted
            : action.Equals("Ignore", StringComparison.OrdinalIgnoreCase)
                ? SuggestionStatus.Ignored
                : throw new ArgumentException($"不支持的操作: {action}，仅支持 Accept / Ignore");

        var suggestions = await _repository.GetByIdsAsync(ids, cancellationToken);
        var count = 0;

        foreach (var s in suggestions.Where(s => s.Status == SuggestionStatus.Pending))
        {
            s.Status = targetStatus;
            s.ResolvedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(s, cancellationToken);
            count++;
        }

        return count;
    }

    // ── 映射 ──────────────────────────────────────────────────────────────────

    private static AgentSuggestionResponse ToResponse(AgentSuggestion s) => new()
    {
        Id = s.Id,
        AgentRunId = s.AgentRunId,
        StoryProjectId = s.StoryProjectId,
        Category = s.Category,
        Title = s.Title,
        ContentJson = s.ContentJson,
        Status = s.Status.ToString(),
        TargetEntityId = s.TargetEntityId,
        CreatedAt = s.CreatedAt,
        ResolvedAt = s.ResolvedAt,
    };
}
