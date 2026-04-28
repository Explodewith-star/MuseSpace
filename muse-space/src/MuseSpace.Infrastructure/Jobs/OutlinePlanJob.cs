using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Agents;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Services.Agents;
using MuseSpace.Application.Services.Suggestions;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Hangfire Job：根据项目设定和用户目标生成章节大纲。
/// 支持全新规划（mode=new）和续写扩展（mode=continue）两种模式。
/// 结果以一条 AgentSuggestion（category=Outline）写入建议表。
/// </summary>
public sealed class OutlinePlanJob
{
    private readonly IAgentRunner _agentRunner;
    private readonly IChapterRepository _chapterRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly IWorldRuleRepository _worldRuleRepo;
    private readonly AgentSuggestionAppService _suggestionService;
    private readonly IAgentProgressNotifier _progressNotifier;
    private readonly LlmProviderSelector _selector;
    private readonly MuseSpaceDbContext _db;
    private readonly ILogger<OutlinePlanJob> _logger;

    public OutlinePlanJob(
        IAgentRunner agentRunner,
        IChapterRepository chapterRepo,
        ICharacterRepository characterRepo,
        IWorldRuleRepository worldRuleRepo,
        AgentSuggestionAppService suggestionService,
        IAgentProgressNotifier progressNotifier,
        LlmProviderSelector selector,
        MuseSpaceDbContext db,
        ILogger<OutlinePlanJob> logger)
    {
        _agentRunner = agentRunner;
        _chapterRepo = chapterRepo;
        _characterRepo = characterRepo;
        _worldRuleRepo = worldRuleRepo;
        _suggestionService = suggestionService;
        _progressNotifier = progressNotifier;
        _selector = selector;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 执行大纲规划。
    /// </summary>
    /// <param name="projectId">项目 ID。</param>
    /// <param name="goal">用户描述的故事目标。</param>
    /// <param name="chapterCount">期望生成的章节数量。</param>
    /// <param name="mode">"new" = 全新规划，"continue" = 续写扩展。</param>
    /// <param name="userId">触发用户 ID。</param>
    public async Task ExecuteAsync(Guid projectId, string goal, int chapterCount, string mode, Guid? userId)
    {
        const string taskType = "outline";

        _logger.LogInformation("[OutlinePlan] Start for project {ProjectId}, mode={Mode}, chapterCount={Count}",
            projectId, mode, chapterCount);

        // 加载用户 LLM 偏好（Hangfire 无 HTTP 上下文，需手动应用）
        await ApplyUserLlmPreferenceAsync(userId);

        await _progressNotifier.NotifyStartedAsync(projectId, taskType);

        try
        {
            // 1. 收集项目上下文
            var characters = await _characterRepo.GetByProjectAsync(projectId);
            var worldRules = await _worldRuleRepo.GetByProjectAsync(projectId);
            var existingChapters = await _chapterRepo.GetByProjectAsync(projectId);
            var orderedChapters = existingChapters.OrderBy(c => c.Number).ToList();

            // 2. 组装上下文文本
            var contextParts = new List<string>();

            if (characters.Count > 0)
            {
                var charText = string.Join("\n", characters.Select(c =>
                    $"- {c.Name}（{c.Role ?? "未定义角色"}）: {c.PersonalitySummary ?? "无描述"}"));
                contextParts.Add($"## 已有角色（{characters.Count} 位）\n\n{charText}");
            }

            if (worldRules.Count > 0)
            {
                var rulesText = string.Join("\n", worldRules.Select(r =>
                    $"- {r.Title}: {r.Description ?? "无描述"}" +
                    (r.IsHardConstraint ? " [硬约束]" : "")));
                contextParts.Add($"## 世界观规则（{worldRules.Count} 条）\n\n{rulesText}");
            }

            // 续写模式：将已有章节作为上下文
            int startNumber = 1;
            if (mode == "continue" && orderedChapters.Count > 0)
            {
                startNumber = orderedChapters.Max(c => c.Number) + 1;
                var chaptersText = string.Join("\n", orderedChapters.Select(c =>
                    $"- 第{c.Number}章 {c.Title ?? "无标题"}: {c.Summary ?? c.Goal ?? "无摘要"}"));
                contextParts.Add($"## 已有章节（{orderedChapters.Count} 章）\n\n{chaptersText}");
            }

            var contextBlock = contextParts.Count > 0
                ? string.Join("\n\n", contextParts) + "\n\n"
                : "";

            // 3. 构造 prompt
            var modeDescription = mode == "continue"
                ? $"请接续已有的 {orderedChapters.Count} 章内容，从第 {startNumber} 章开始续写规划。"
                : $"请从第 1 章开始进行全新规划。";

            var userPrompt = $"""
                {contextBlock}## 规划要求

                {modeDescription}

                故事目标：{goal}
                需要生成的章节数量：{chapterCount}

                请根据以上信息生成结构化的章节大纲。
                """;

            // 4. 调用 Agent
            await _progressNotifier.NotifyGeneratingAsync(projectId, taskType);

            var agentContext = new AgentRunContext
            {
                UserId = userId,
                ProjectId = projectId,
            };

            var result = await _agentRunner.RunAsync(
                OutlinePlanAgentDefinition.AgentName,
                userPrompt,
                agentContext);

            if (!result.Success)
            {
                _logger.LogWarning("[OutlinePlan] Agent failed for project {ProjectId}: {Error}",
                    projectId, result.ErrorMessage);
                await _progressNotifier.NotifyFailedAsync(projectId, taskType,
                    result.ErrorMessage ?? "Agent 执行失败");
                return;
            }

            // 5. 解析章节数组 JSON
            var json = result.Output.Trim();
            if (json.StartsWith("```"))
                json = Regex.Replace(json, @"```\w*\n?", "").Trim('`').Trim();

            List<OutlineChapterItem> items;
            try
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                items = JsonSerializer.Deserialize<List<OutlineChapterItem>>(json, opts) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[OutlinePlan] Failed to parse Agent output for project {ProjectId}", projectId);
                await _progressNotifier.NotifyFailedAsync(projectId, taskType, "大纲 JSON 解析失败");
                return;
            }

            if (items.Count == 0)
            {
                _logger.LogWarning("[OutlinePlan] Agent returned empty outline for project {ProjectId}", projectId);
                await _progressNotifier.NotifyFailedAsync(projectId, taskType, "Agent 返回了空大纲");
                return;
            }

            // 6. 修正章节编号（续写模式从 startNumber 开始）
            if (mode == "continue")
            {
                for (var i = 0; i < items.Count; i++)
                    items[i].Number = startNumber + i;
            }

            // 7. 写入一条 Outline 类型建议
            var contentJson = JsonSerializer.Serialize(items, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
            });

            var totalChapters = items.Count;
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
            var modeLabel = mode == "continue" ? "续写" : "全新";

            await _suggestionService.CreateAsync(
                agentRunId: agentContext.RunId,
                storyProjectId: projectId,
                category: SuggestionCategories.Outline,
                title: $"大纲草案（{modeLabel}·{totalChapters}章·{timestamp}）",
                contentJson: contentJson);

            _logger.LogInformation("[OutlinePlan] Saved outline with {Count} chapters for project {ProjectId}",
                totalChapters, projectId);

            await _progressNotifier.NotifyDoneAsync(projectId, taskType,
                $"大纲已生成，共 {totalChapters} 章");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OutlinePlan] Unexpected error for project {ProjectId}", projectId);
            await _progressNotifier.NotifyFailedAsync(projectId, taskType, "生成过程发生意外错误");
        }
    }

    private sealed class OutlineChapterItem
    {
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>
    /// 在后台 Job 中根据 userId 加载用户 LLM 偏好并应用到 selector。
    /// </summary>
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
