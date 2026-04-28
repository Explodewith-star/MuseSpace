using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Agents;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Services.Agents;
using MuseSpace.Application.Services.Suggestions;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Hangfire Job：对草稿文本进行角色一致性检查。
/// 由草稿生成完成后异步触发，不阻塞主生成链路。
/// </summary>
public sealed class CharacterConsistencyCheckJob
{
    private readonly IAgentRunner _agentRunner;
    private readonly ICharacterRepository _characterRepo;
    private readonly AgentSuggestionAppService _suggestionService;
    private readonly LlmProviderSelector _selector;
    private readonly MuseSpaceDbContext _db;
    private readonly ILogger<CharacterConsistencyCheckJob> _logger;

    public CharacterConsistencyCheckJob(
        IAgentRunner agentRunner,
        ICharacterRepository characterRepo,
        AgentSuggestionAppService suggestionService,
        LlmProviderSelector selector,
        MuseSpaceDbContext db,
        ILogger<CharacterConsistencyCheckJob> logger)
    {
        _agentRunner = agentRunner;
        _characterRepo = characterRepo;
        _suggestionService = suggestionService;
        _selector = selector;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 执行角色一致性检查。
    /// </summary>
    /// <param name="projectId">项目 ID。</param>
    /// <param name="draftText">待检查的草稿文本。</param>
    /// <param name="userId">触发用户 ID（可选）。</param>
    public async Task ExecuteAsync(Guid projectId, string draftText, Guid? userId)
    {
        _logger.LogInformation("[CharacterConsistency] Start for project {ProjectId}, text length {Length}",
            projectId, draftText.Length);

        // 加载用户 LLM 偏好（Hangfire 无 HTTP 上下文，需手动应用）
        await ApplyUserLlmPreferenceAsync(userId);

        // 1. 加载该项目的所有角色
        var characters = await _characterRepo.GetByProjectAsync(projectId);
        if (characters.Count == 0)
        {
            _logger.LogInformation("[CharacterConsistency] No characters for project {ProjectId}, skip", projectId);
            return;
        }

        // 2. 组装角色卡文本
        var charactersText = string.Join("\n\n", characters.Select((c, i) =>
        {
            var lines = new List<string>
            {
                $"[角色{i + 1}] {c.Name}"
            };
            if (!string.IsNullOrWhiteSpace(c.Role)) lines.Add($"  角色定位: {c.Role}");
            if (c.Age.HasValue) lines.Add($"  年龄: {c.Age}");
            if (!string.IsNullOrWhiteSpace(c.PersonalitySummary)) lines.Add($"  性格: {c.PersonalitySummary}");
            if (!string.IsNullOrWhiteSpace(c.Motivation)) lines.Add($"  动机: {c.Motivation}");
            if (!string.IsNullOrWhiteSpace(c.SpeakingStyle)) lines.Add($"  说话风格: {c.SpeakingStyle}");
            if (!string.IsNullOrWhiteSpace(c.ForbiddenBehaviors)) lines.Add($"  禁止行为: {c.ForbiddenBehaviors}");
            if (!string.IsNullOrWhiteSpace(c.CurrentState)) lines.Add($"  当前状态: {c.CurrentState}");
            return string.Join("\n", lines);
        }));

        // 3. 调用角色一致性 Agent
        var agentContext = new AgentRunContext
        {
            UserId = userId,
            ProjectId = projectId,
        };

        var userPrompt = $"""
            ## 角色设定

            {charactersText}

            ## 待检查草稿

            {draftText}

            请对照上述角色设定，分析草稿中角色的行为、对话和状态是否与设定存在冲突或矛盾。
            """;

        var result = await _agentRunner.RunAsync(
            CharacterConsistencyAgentDefinition.AgentName,
            userPrompt,
            agentContext);

        if (!result.Success)
        {
            _logger.LogWarning("[CharacterConsistency] Agent failed for project {ProjectId}: {Error}",
                projectId, result.ErrorMessage);
            return;
        }

        // 4. 解析冲突列表 JSON
        var json = result.Output.Trim();
        if (json.StartsWith("```"))
            json = Regex.Replace(json, @"```\w*\n?", "").Trim('`').Trim();

        List<CharacterConflictItem> items;
        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            items = JsonSerializer.Deserialize<List<CharacterConflictItem>>(json, opts) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CharacterConsistency] Failed to parse Agent output for project {ProjectId}", projectId);
            return;
        }

        if (items.Count == 0)
        {
            _logger.LogInformation("[CharacterConsistency] No conflicts found for project {ProjectId}", projectId);
            return;
        }

        // 5. 每条冲突写入 agent_suggestions
        foreach (var item in items)
        {
            var contentJson = JsonSerializer.Serialize(item, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
            });

            // 查找涉及的角色实体 ID（用于 TargetEntityId）
            var matchedChar = characters.FirstOrDefault(c =>
                string.Equals(c.Name, item.CharacterName, StringComparison.OrdinalIgnoreCase));

            await _suggestionService.CreateAsync(
                agentRunId: agentContext.RunId,
                storyProjectId: projectId,
                category: SuggestionCategories.Character,
                title: $"角色冲突：{item.CharacterName} - {item.ConflictType}",
                contentJson: contentJson,
                targetEntityId: matchedChar?.Id);

            _logger.LogDebug("[CharacterConsistency] Saved conflict for character {CharacterName}, severity {Severity}",
                item.CharacterName, item.Severity);
        }

        _logger.LogInformation("[CharacterConsistency] Saved {Count} conflicts for project {ProjectId}",
            items.Count, projectId);
    }

    private sealed class CharacterConflictItem
    {
        public string CharacterName { get; set; } = string.Empty;
        public string ConflictType { get; set; } = string.Empty;
        public string Severity { get; set; } = "medium";
        public string ConflictSnippet { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
    }

    private async Task ApplyUserLlmPreferenceAsync(Guid? userId)
    {
        if (userId is null) return;

        var pref = await _db.UserLlmPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId.Value);

        if (pref is null) return;

        if (Enum.TryParse<LlmProviderType>(pref.Provider, ignoreCase: true, out var provider))
            _selector.Active = provider;

        if (!string.IsNullOrWhiteSpace(pref.ModelId))
            _selector.ActiveModel = pref.ModelId;
    }
}
