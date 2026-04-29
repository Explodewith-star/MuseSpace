using System.Text.Json;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Abstractions.Suggestions;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Services.Suggestions;

/// <summary>
/// 把 Category=WorldRule 的建议写入 world_rules 表。
/// </summary>
public sealed class WorldRuleSuggestionApplier : ISuggestionApplier
{
    private readonly IWorldRuleRepository _worldRuleRepository;

    public WorldRuleSuggestionApplier(IWorldRuleRepository worldRuleRepository)
        => _worldRuleRepository = worldRuleRepository;

    public string Category => SuggestionCategories.WorldRule;

    public async Task<Guid> ApplyAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<WorldRulePayload>(suggestion.ContentJson, opts)
            ?? throw new InvalidOperationException("建议内容 JSON 解析失败");

        var rule = new WorldRule
        {
            Id = suggestion.TargetEntityId ?? Guid.NewGuid(),
            StoryProjectId = suggestion.StoryProjectId,
            Title = data.Title ?? "未命名规则",
            Category = data.Category,
            Description = data.Description,
            Priority = data.Priority,
            IsHardConstraint = data.IsHardConstraint,
        };

        await _worldRuleRepository.SaveAsync(suggestion.StoryProjectId, rule, cancellationToken);
        return rule.Id;
    }

    public async Task RetractAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        if (suggestion.TargetEntityId.HasValue)
            await _worldRuleRepository.DeleteAsync(suggestion.StoryProjectId, suggestion.TargetEntityId.Value, cancellationToken);
    }

    private sealed class WorldRulePayload
    {
        public string? Title { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public int Priority { get; set; } = 3;
        public bool IsHardConstraint { get; set; }
    }
}
