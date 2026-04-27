using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Agents;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Services.Agents;
using MuseSpace.Application.Services.Suggestions;
using MuseSpace.Contracts.Suggestions;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Hangfire Job：对草稿文本进行世界观一致性检查。
/// 由草稿生成完成后异步触发，不阻塞主生成链路。
/// </summary>
public sealed class ConsistencyCheckJob
{
    private readonly IAgentRunner _agentRunner;
    private readonly IWorldRuleRepository _worldRuleRepo;
    private readonly AgentSuggestionAppService _suggestionService;
    private readonly ILogger<ConsistencyCheckJob> _logger;

    public ConsistencyCheckJob(
        IAgentRunner agentRunner,
        IWorldRuleRepository worldRuleRepo,
        AgentSuggestionAppService suggestionService,
        ILogger<ConsistencyCheckJob> logger)
    {
        _agentRunner = agentRunner;
        _worldRuleRepo = worldRuleRepo;
        _suggestionService = suggestionService;
        _logger = logger;
    }

    /// <summary>
    /// 执行一致性检查。
    /// </summary>
    /// <param name="projectId">项目 ID。</param>
    /// <param name="draftText">待检查的草稿文本。</param>
    /// <param name="userId">触发用户 ID（可选）。</param>
    public async Task ExecuteAsync(Guid projectId, string draftText, Guid? userId)
    {
        _logger.LogInformation("[ConsistencyCheck] Start for project {ProjectId}, text length {Length}",
            projectId, draftText.Length);

        // 1. 加载该项目的世界观规则
        var rules = await _worldRuleRepo.GetByProjectAsync(projectId);
        if (rules.Count == 0)
        {
            _logger.LogInformation("[ConsistencyCheck] No world rules for project {ProjectId}, skip", projectId);
            return;
        }

        // 2. 组装世界观规则文本
        var rulesText = string.Join("\n\n", rules.Select((r, i) =>
            $"[规则{i + 1}] {r.Title}" +
            $"\n  类别: {r.Category ?? "通用"}" +
            $"\n  硬约束: {(r.IsHardConstraint ? "是" : "否")}" +
            $"\n  优先级: {r.Priority}" +
            $"\n  描述: {r.Description ?? "无"}"));

        // 3. 调用一致性检查 Agent
        var agentContext = new AgentRunContext
        {
            UserId = userId,
            ProjectId = projectId,
        };

        var userPrompt = $"""
            ## 世界观规则

            {rulesText}

            ## 待检查草稿

            {draftText}

            请逐条对照上述世界观规则，分析草稿中是否存在冲突或矛盾。
            """;

        var result = await _agentRunner.RunAsync(
            ConsistencyCheckAgentDefinition.AgentName,
            userPrompt,
            agentContext);

        if (!result.Success)
        {
            _logger.LogWarning("[ConsistencyCheck] Agent failed for project {ProjectId}: {Error}",
                projectId, result.ErrorMessage);
            return;
        }

        // 4. 解析冲突列表 JSON
        var json = result.Output.Trim();
        if (json.StartsWith("```"))
            json = Regex.Replace(json, @"```\w*\n?", "").Trim('`').Trim();

        List<ConsistencyItem> items;
        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            items = JsonSerializer.Deserialize<List<ConsistencyItem>>(json, opts) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ConsistencyCheck] Failed to parse Agent output for project {ProjectId}", projectId);
            return;
        }

        if (items.Count == 0)
        {
            _logger.LogInformation("[ConsistencyCheck] No conflicts found for project {ProjectId}", projectId);
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

            await _suggestionService.CreateAsync(
                agentRunId: agentContext.RunId,
                storyProjectId: projectId,
                category: SuggestionCategories.Consistency,
                title: $"世界观冲突：{item.RuleName}",
                contentJson: contentJson);
        }

        _logger.LogInformation("[ConsistencyCheck] Found {Count} conflicts for project {ProjectId}",
            items.Count, projectId);
    }

    /// <summary>Agent 输出的单条冲突结构。</summary>
    private sealed class ConsistencyItem
    {
        public string RuleName { get; set; } = string.Empty;
        public string Severity { get; set; } = "medium";
        public string ConflictSnippet { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
    }
}
