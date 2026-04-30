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
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Hangfire Job：对 Outline 建议中的指定卷进行重做，保留其它卷不变。
/// </summary>
public sealed class RegenerateOutlineVolumeJob
{
    private readonly IAgentRunner _agentRunner;
    private readonly IAgentSuggestionRepository _suggestionRepo;
    private readonly IAgentProgressNotifier _progressNotifier;
    private readonly LlmProviderSelector _selector;
    private readonly MuseSpaceDbContext _db;
    private readonly ILogger<RegenerateOutlineVolumeJob> _logger;

    public RegenerateOutlineVolumeJob(
        IAgentRunner agentRunner,
        IAgentSuggestionRepository suggestionRepo,
        IAgentProgressNotifier progressNotifier,
        LlmProviderSelector selector,
        MuseSpaceDbContext db,
        ILogger<RegenerateOutlineVolumeJob> logger)
    {
        _agentRunner = agentRunner;
        _suggestionRepo = suggestionRepo;
        _progressNotifier = progressNotifier;
        _selector = selector;
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid suggestionId, int volumeNumber, string? extraInstruction, Guid? userId)
    {
        const string taskType = "outline-volume-regenerate";

        var suggestion = await _suggestionRepo.GetByIdAsync(suggestionId);
        if (suggestion is null || suggestion.Category != SuggestionCategories.Outline)
        {
            _logger.LogWarning("[RegenerateVolume] Suggestion {Id} not found or not Outline", suggestionId);
            return;
        }

        var projectId = suggestion.StoryProjectId;
        await ApplyUserLlmPreferenceAsync(userId);
        await _progressNotifier.NotifyStartedAsync(projectId, taskType);

        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var payload = JsonSerializer.Deserialize<OutlinePayload>(suggestion.ContentJson, opts);
            if (payload is null || payload.Volumes.Count == 0)
            {
                await _progressNotifier.NotifyFailedAsync(projectId, taskType, "原大纲格式异常，无法重做");
                return;
            }

            var target = payload.Volumes.FirstOrDefault(v => v.Number == volumeNumber);
            if (target is null)
            {
                await _progressNotifier.NotifyFailedAsync(projectId, taskType, $"未找到第 {volumeNumber} 卷");
                return;
            }

            // 上下文：其它卷的概览，让模型理解前后衔接
            var siblingsText = string.Join("\n", payload.Volumes
                .Where(v => v.Number != volumeNumber)
                .OrderBy(v => v.Number)
                .Select(v =>
                {
                    var range = v.Chapters.Count > 0
                        ? $"第{v.Chapters.Min(c => c.Number)}~{v.Chapters.Max(c => c.Number)}章"
                        : "未排章";
                    return $"- 卷{v.Number}《{v.Title}》（{range}，{v.Chapters.Count}章）：{v.Theme}";
                }));

            var targetChapterCount = Math.Max(target.Chapters.Count, 3);
            var instructionLine = string.IsNullOrWhiteSpace(extraInstruction)
                ? ""
                : $"\n## 用户附加要求\n\n{extraInstruction}\n";

            var userPrompt = $$"""
                ## 已有分卷概览

                {{(string.IsNullOrWhiteSpace(siblingsText) ? "（无其它卷）" : siblingsText)}}

                ## 重做要求

                请仅为「卷{{target.Number}}《{{target.Title}}》」重新生成 {{targetChapterCount}} 个章节。
                本卷主题：{{target.Theme}}
                请在 chapters 中输出本卷的章节，章节序号 number 从 1 开始连续编号即可（系统会重排）。
                必须返回完整的 volumes 结构（其中只包含被重做的这一卷），格式：
                { "volumes": [ { "number": {{target.Number}}, "title": "...", "theme": "...", "chapters": [...] } ] }
                {{instructionLine}}
                """;

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
                _logger.LogWarning("[RegenerateVolume] Agent failed: {Error}", result.ErrorMessage);
                await _progressNotifier.NotifyFailedAsync(projectId, taskType, result.ErrorMessage ?? "Agent 执行失败");
                return;
            }

            // 解析 Agent 输出（容错：LLM 有时夹杂占位字符串）
            var json = result.Output.Trim();
            if (json.StartsWith("```"))
                json = Regex.Replace(json, @"```\w*\n?", "").Trim('`').Trim();
            if (!json.StartsWith("{"))
            {
                var m = Regex.Match(json, @"\{[\s\S]+\}", RegexOptions.Singleline);
                if (m.Success) json = m.Value;
            }

            List<OutlineVolumeItem> parsedVolumes;
            try
            {
                parsedVolumes = ParseVolumesTolerant(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RegenerateVolume] Failed to parse Agent output");
                await _progressNotifier.NotifyFailedAsync(projectId, taskType, "重做结果 JSON 解析失败");
                return;
            }

            var newVolume = parsedVolumes.FirstOrDefault();
            if (newVolume is null || newVolume.Chapters.Count == 0)
            {
                await _progressNotifier.NotifyFailedAsync(projectId, taskType, "Agent 返回了空卷");
                return;
            }

            // 替换原卷
            target.Title = string.IsNullOrWhiteSpace(newVolume.Title) ? target.Title : newVolume.Title;
            target.Theme = string.IsNullOrWhiteSpace(newVolume.Theme) ? target.Theme : newVolume.Theme;
            target.Chapters = newVolume.Chapters;

            // 重排所有章节 number
            var orderedVolumes = payload.Volumes.OrderBy(v => v.Number).ToList();
            var nextNumber = 1;
            foreach (var vol in orderedVolumes)
                foreach (var ch in vol.Chapters)
                    ch.Number = nextNumber++;

            payload.Volumes = orderedVolumes;

            // 写回
            suggestion.ContentJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
            });
            await _suggestionRepo.UpdateAsync(suggestion);

            _logger.LogInformation("[RegenerateVolume] Updated volume {Number} for suggestion {Id}",
                volumeNumber, suggestionId);

            // 链式触发大纲一致性预检（仅重做卷内容）
            var redoneText = string.Join("\n\n", target.Chapters.Select(c =>
                $"第{c.Number}章 {c.Title}\n目标：{c.Goal}\n摘要：{c.Summary}"));
            BackgroundJob.Enqueue<ConsistencyCheckJob>(j =>
                j.ExecuteAsync(projectId, redoneText, userId, $"大纲卷{volumeNumber}世界观", SuggestionCategories.OutlineConsistency));

            await _progressNotifier.NotifyDoneAsync(projectId, taskType,
                $"卷 {volumeNumber} 已重做完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RegenerateVolume] Unexpected error for suggestion {Id}", suggestionId);
            await _progressNotifier.NotifyFailedAsync(projectId, taskType, "重做过程发生意外错误");
        }
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

    private static List<OutlineVolumeItem> ParseVolumesTolerant(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

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
            if (volEl.ValueKind != JsonValueKind.Object) continue;

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
}
