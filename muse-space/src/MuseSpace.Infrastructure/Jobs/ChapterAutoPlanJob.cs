using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Agents;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Services.Agents;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Hangfire Job：根据章节大纲自动生成本章节的写作计划字段（冲突/情感曲线/关键角色/必中要点）。
/// 直接写回 Chapter 实体（非建议层），以便随后用于草稿生成。
/// </summary>
public sealed class ChapterAutoPlanJob
{
    private const string TaskType = "chapter-auto-plan";

    private readonly IAgentRunner _agentRunner;
    private readonly IChapterRepository _chapterRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly IAgentProgressNotifier _progressNotifier;
    private readonly LlmProviderSelector _selector;
    private readonly MuseSpaceDbContext _db;
    private readonly ILogger<ChapterAutoPlanJob> _logger;

    public ChapterAutoPlanJob(
        IAgentRunner agentRunner,
        IChapterRepository chapterRepo,
        ICharacterRepository characterRepo,
        IAgentProgressNotifier progressNotifier,
        LlmProviderSelector selector,
        MuseSpaceDbContext db,
        ILogger<ChapterAutoPlanJob> logger)
    {
        _agentRunner = agentRunner;
        _chapterRepo = chapterRepo;
        _characterRepo = characterRepo;
        _progressNotifier = progressNotifier;
        _selector = selector;
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid projectId, Guid chapterId, Guid? userId)
    {
        _logger.LogInformation("[ChapterAutoPlan] Start chapter={ChapterId}", chapterId);

        await ApplyUserLlmPreferenceAsync(userId);
        await _progressNotifier.NotifyStartedAsync(projectId, TaskType);

        try
        {
            var chapter = await _chapterRepo.GetByIdAsync(projectId, chapterId);
            if (chapter is null)
            {
                await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "章节不存在");
                return;
            }

            var characters = await _characterRepo.GetByProjectAsync(projectId);

            var charText = characters.Count == 0
                ? "（无）"
                : string.Join("\n", characters.Select(c =>
                    $"- {c.Id} | {c.Name}（{c.Role ?? "未定义"}）"));

            var prompt = $$"""
                ## 可用角色

                {{charText}}

                ## 章节大纲条目

                第{{chapter.Number}}章 {{chapter.Title ?? "（无标题）"}}
                目标：{{chapter.Goal ?? "（无）"}}
                摘要：{{chapter.Summary ?? "（无）"}}

                请基于以上信息产出本章节的写作计划。
                """;

            await _progressNotifier.NotifyGeneratingAsync(projectId, TaskType);

            var ctx = new AgentRunContext { UserId = userId, ProjectId = projectId };
            var result = await _agentRunner.RunAsync(
                ChapterPlanGenerationAgentDefinition.AgentName, prompt, ctx);

            if (!result.Success)
            {
                await _progressNotifier.NotifyFailedAsync(projectId, TaskType,
                    result.ErrorMessage ?? "Agent 执行失败");
                return;
            }

            var json = result.Output.Trim();
            if (json.StartsWith("```"))
                json = Regex.Replace(json, @"```\w*\n?", "").Trim('`').Trim();

            ChapterPlanPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<ChapterPlanPayload>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ChapterAutoPlan] JSON parse failed");
                await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "章节计划 JSON 解析失败");
                return;
            }

            if (payload is null)
            {
                await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "Agent 返回空计划");
                return;
            }

            // 仅采纳在可用角色集合中的 id
            var validCharIds = characters.Select(c => c.Id).ToHashSet();
            var keyIds = (payload.KeyCharacterIds ?? new List<string>())
                .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty && validCharIds.Contains(g))
                .Distinct()
                .ToList();

            chapter.Conflict = payload.Conflict;
            chapter.EmotionCurve = payload.EmotionCurve;
            chapter.KeyCharacterIds = keyIds;
            chapter.MustIncludePoints = payload.MustIncludePoints ?? new List<string>();

            await _chapterRepo.SaveAsync(projectId, chapter);

            _logger.LogInformation("[ChapterAutoPlan] Updated chapter {ChapterId}", chapterId);
            await _progressNotifier.NotifyDoneAsync(projectId, TaskType,
                $"第 {chapter.Number} 章 计划已自动填充");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ChapterAutoPlan] Unexpected error");
            await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "自动规划过程发生意外错误");
        }
    }

    private async Task ApplyUserLlmPreferenceAsync(Guid? userId)
    {
        if (userId is null) return;
        var pref = await _db.UserLlmPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId.Value);
        if (pref is null) return;
        if (Enum.TryParse<LlmProviderType>(pref.Provider, ignoreCase: true, out var provider))
            _selector.Active = provider;
        if (!string.IsNullOrWhiteSpace(pref.ModelId))
            _selector.ActiveModel = pref.ModelId;
    }

    private sealed class ChapterPlanPayload
    {
        public string? Conflict { get; set; }
        public string? EmotionCurve { get; set; }
        public List<string>? KeyCharacterIds { get; set; }
        public List<string>? MustIncludePoints { get; set; }
    }
}
