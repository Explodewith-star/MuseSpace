using System.Text.Json;
using System.Text.RegularExpressions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Agents;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Services.Agents;
using MuseSpace.Application.Services.Suggestions;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Domain.Enums;
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
    private readonly ITaskProgressService _taskProgress;
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
        ITaskProgressService taskProgress,
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
        _taskProgress = taskProgress;
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

        var modeLabel = mode switch
        {
            "continue" => "续写",
            "extra" => "番外",
            _ => "全新",
        };
        var bgTaskId = await _taskProgress.StartAsync(
            userId, projectId, BackgroundTaskType.OutlinePlanning,
            $"AI 大纲规划（{modeLabel}）");

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
            if ((mode == "continue" || mode == "extra") && orderedChapters.Count > 0)
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
            var modeDescription = mode switch
            {
                "continue" => $"请接续已有的 {orderedChapters.Count} 章内容，从第 {startNumber} 章开始续写规划。",
                "extra" => $"请基于已有项目设定，规划一段【番外】支线，从第 {startNumber} 章开始；可独立成卷，主题应区别于主线。",
                _ => "请从第 1 章开始进行全新规划。",
            };

            var userPrompt = $"""
                {contextBlock}## 规划要求

                {modeDescription}

                故事目标：{goal}
                需要生成的章节数量：{chapterCount}

                请根据以上信息生成结构化的章节大纲。
                """;

            // 4. 调用 Agent
            await _progressNotifier.NotifyGeneratingAsync(projectId, taskType);
            await _taskProgress.ReportProgressAsync(bgTaskId, 30, "正在调用 AI 生成大纲…");

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
                var err = result.ErrorMessage ?? "Agent 执行失败";
                await _progressNotifier.NotifyFailedAsync(projectId, taskType, err);
                await _taskProgress.FailAsync(bgTaskId, err);
                return;
            }

            // 5. 解析分卷 JSON（LLM 有时在数组中输出占位字符串或注释，用 JsonDocument 容错解析）
            var json = result.Output.Trim();
            if (json.StartsWith("```"))
                json = Regex.Replace(json, @"```\w*\n?", "").Trim('`').Trim();

            // 若 JSON 前后有说明文字，尝试提取第一个完整 {...} 块
            if (!json.StartsWith("{"))
            {
                var m = Regex.Match(json, @"\{[\s\S]+\}", RegexOptions.Singleline);
                if (m.Success) json = m.Value;
            }

            List<OutlineVolumeItem> volumes;
            try
            {
                volumes = ParseVolumesTolerant(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[OutlinePlan] Failed to parse Agent output for project {ProjectId}", projectId);
                await _progressNotifier.NotifyFailedAsync(projectId, taskType, "大纲 JSON 解析失败");
                await _taskProgress.FailAsync(bgTaskId, "大纲 JSON 解析失败");
                return;
            }
            if (volumes.Count == 0 || volumes.All(v => v.Chapters.Count == 0))
            {
                _logger.LogWarning("[OutlinePlan] Agent returned empty outline for project {ProjectId}", projectId);
                await _progressNotifier.NotifyFailedAsync(projectId, taskType, "Agent 返回了空大纲");
                await _taskProgress.FailAsync(bgTaskId, "Agent 返回了空大纲");
                return;
            }

            // 6. 修正章节编号（跨卷连续递增；续写/番外模式从 startNumber 开始）
            var nextNumber = (mode == "continue" || mode == "extra") ? startNumber : 1;
            var totalChapters = 0;
            for (var vi = 0; vi < volumes.Count; vi++)
            {
                var vol = volumes[vi];
                if (vol.Number == 0) vol.Number = vi + 1;
                foreach (var ch in vol.Chapters)
                {
                    ch.Number = nextNumber++;
                    totalChapters++;
                }
            }

            // 7. 序列化并写入 Outline 类型建议
            var contentJson = JsonSerializer.Serialize(new { volumes }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
            });

            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
            var modeLabelDisplay = mode switch
            {
                "continue" => "续写",
                "extra" => "番外",
                _ => "全新",
            };

            await _suggestionService.CreateAsync(
                agentRunId: agentContext.RunId,
                storyProjectId: projectId,
                category: SuggestionCategories.Outline,
                title: $"大纲草案（{modeLabelDisplay}·{totalChapters}章·{timestamp}）",
                contentJson: contentJson);

            _logger.LogInformation("[OutlinePlan] Saved outline with {Count} chapters for project {ProjectId}",
                totalChapters, projectId);

            await _taskProgress.ReportProgressAsync(bgTaskId, 85, $"已生成 {totalChapters} 章大纲，正在预检…");

            // 8. 链式触发大纲一致性预检（仅当项目存在世界观规则时）
            // 将所有章节 title+goal+summary 拼接为伪草稿，复用现有 ConsistencyCheckJob。
            // 结果写入 OutlineConsistency 类目，标题前缀为"大纲世界观冲突"。
            if (worldRules.Count > 0)
            {
                var outlineDraftText = string.Join("\n\n", volumes.SelectMany(v =>
                    v.Chapters.Select(c =>
                        $"第{c.Number}章 {c.Title}\n目标：{c.Goal}\n摘要：{c.Summary}")));
                BackgroundJob.Enqueue<ConsistencyCheckJob>(j =>
                    j.ExecuteAsync(projectId, outlineDraftText, userId, "大纲世界观", SuggestionCategories.OutlineConsistency));
                _logger.LogInformation("[OutlinePlan] Enqueued outline consistency precheck for project {ProjectId}", projectId);
            }

            await _progressNotifier.NotifyDoneAsync(projectId, taskType,
                $"大纲已生成，共 {totalChapters} 章");
            await _taskProgress.CompleteAsync(bgTaskId, $"大纲已生成，共 {totalChapters} 章");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OutlinePlan] Unexpected error for project {ProjectId}", projectId);
            await _progressNotifier.NotifyFailedAsync(projectId, taskType, "生成过程发生意外错误");
            await _taskProgress.FailAsync(bgTaskId, ex.Message);
        }
    }

    private sealed class OutlinePayload
    {
        public List<OutlineVolumeItem> Volumes { get; set; } = [];
    }

    private sealed class OutlineVolumeItem
    {
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public List<OutlineChapterItem> Chapters { get; set; } = [];
    }

    private sealed class OutlineChapterItem
    {
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>
    /// 容错解析 volumes JSON。
    /// LLM 有时在 volumes 数组中夹杂字符串占位符（如 "..."）、null 或注释，
    /// 使用 JsonDocument 逐元素解析，自动跳过非对象项。
    /// </summary>
    private static List<OutlineVolumeItem> ParseVolumesTolerant(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 允许顶层直接是 volumes 数组，也允许包裹在 { volumes: [...] } 中
        JsonElement volumesEl;
        if (root.ValueKind == JsonValueKind.Array)
        {
            volumesEl = root;
        }
        else if (root.ValueKind == JsonValueKind.Object
                 && root.TryGetProperty("volumes", out var ve))
        {
            volumesEl = ve;
        }
        else
        {
            return [];
        }

        if (volumesEl.ValueKind != JsonValueKind.Array) return [];

        var volumes = new List<OutlineVolumeItem>();
        var volIdx = 0;
        foreach (var volEl in volumesEl.EnumerateArray())
        {
            volIdx++;
            if (volEl.ValueKind != JsonValueKind.Object) continue; // 跳过 "..." 等非对象

            var vol = new OutlineVolumeItem
            {
                Number = volEl.TryGetProperty("number", out var n) && n.TryGetInt32(out var ni) ? ni : volIdx,
                Title = volEl.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : string.Empty,
                Theme = volEl.TryGetProperty("theme", out var th) && th.ValueKind == JsonValueKind.String ? th.GetString()! : string.Empty,
            };

            if (volEl.TryGetProperty("chapters", out var chapEl) && chapEl.ValueKind == JsonValueKind.Array)
            {
                var chIdx = 0;
                foreach (var chEl in chapEl.EnumerateArray())
                {
                    chIdx++;
                    if (chEl.ValueKind != JsonValueKind.Object) continue;

                    vol.Chapters.Add(new OutlineChapterItem
                    {
                        Number = chEl.TryGetProperty("number", out var cn) && cn.TryGetInt32(out var cni) ? cni : chIdx,
                        Title = chEl.TryGetProperty("title", out var ct) && ct.ValueKind == JsonValueKind.String ? ct.GetString()! : string.Empty,
                        Goal = chEl.TryGetProperty("goal", out var cg) && cg.ValueKind == JsonValueKind.String ? cg.GetString()! : string.Empty,
                        Summary = chEl.TryGetProperty("summary", out var cs) && cs.ValueKind == JsonValueKind.String ? cs.GetString()! : string.Empty,
                    });
                }
            }

            volumes.Add(vol);
        }

        return volumes;
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
