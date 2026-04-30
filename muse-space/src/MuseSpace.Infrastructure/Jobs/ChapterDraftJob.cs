using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Hangfire;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Abstractions.Skills;
using MuseSpace.Domain.Enums;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Hangfire Job：基于章节计划生成本章节草稿。
/// 复用现有 SceneDraftSkill（TaskType = "scene-draft"），将章节计划字段拼接进 SceneGoal/Conflict/EmotionCurve。
/// 完成后写回 Chapter.DraftText，Status 升为 Drafting。
/// </summary>
public sealed class ChapterDraftJob
{
    private const string TaskType = "chapter-draft";

    private readonly ISkillOrchestrator _orchestrator;
    private readonly IChapterRepository _chapterRepo;
    private readonly IAgentProgressNotifier _progressNotifier;
    private readonly LlmProviderSelector _selector;
    private readonly MuseSpaceDbContext _db;
    private readonly ILogger<ChapterDraftJob> _logger;

    public ChapterDraftJob(
        ISkillOrchestrator orchestrator,
        IChapterRepository chapterRepo,
        IAgentProgressNotifier progressNotifier,
        LlmProviderSelector selector,
        MuseSpaceDbContext db,
        ILogger<ChapterDraftJob> logger)
    {
        _orchestrator = orchestrator;
        _chapterRepo = chapterRepo;
        _progressNotifier = progressNotifier;
        _selector = selector;
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid projectId, Guid chapterId, Guid? userId)
    {
        _logger.LogInformation("[ChapterDraft] Start chapter={ChapterId}", chapterId);

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

            // SceneGoal 综合：章节标题 + 目标 + 摘要 + 必中要点
            var sceneGoalParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(chapter.Title))
                sceneGoalParts.Add($"【第{chapter.Number}章 {chapter.Title}】");
            if (!string.IsNullOrWhiteSpace(chapter.Goal))
                sceneGoalParts.Add($"章节目标：{chapter.Goal}");
            if (!string.IsNullOrWhiteSpace(chapter.Summary))
                sceneGoalParts.Add($"章节摘要：{chapter.Summary}");
            if (chapter.MustIncludePoints.Count > 0)
                sceneGoalParts.Add("必须命中要点：\n" +
                    string.Join("\n", chapter.MustIncludePoints.Select(p => $"- {p}")));

            var sceneGoal = string.Join("\n", sceneGoalParts);

            var request = new SkillRequest
            {
                TaskType = "scene-draft",
                StoryProjectId = projectId,
                Parameters = new Dictionary<string, string>
                {
                    ["SceneGoal"] = sceneGoal,
                    ["Conflict"] = chapter.Conflict ?? string.Empty,
                    ["EmotionCurve"] = chapter.EmotionCurve ?? string.Empty,
                },
            };

            await _progressNotifier.NotifyGeneratingAsync(projectId, TaskType);

            var result = await _orchestrator.ExecuteAsync(request);

            if (!result.Success)
            {
                await _progressNotifier.NotifyFailedAsync(projectId, TaskType,
                    result.ErrorMessage ?? "草稿生成失败");
                return;
            }

            chapter.DraftText = result.Output;
            if (chapter.Status == ChapterStatus.Planned)
                chapter.Status = ChapterStatus.Drafting;
            await _chapterRepo.SaveAsync(projectId, chapter);

            _logger.LogInformation("[ChapterDraft] Saved draft for chapter {ChapterId}, len={Len}",
                chapterId, result.Output.Length);
            await _progressNotifier.NotifyDoneAsync(projectId, TaskType,
                $"第 {chapter.Number} 章 草稿已生成");

            // 链式触发：文风一致性 + 角色一致性 + 伏笔追踪
            BackgroundJob.Enqueue<StyleConsistencyCheckJob>(
                j => j.ExecuteAsync(projectId, chapterId, result.Output, userId));
            BackgroundJob.Enqueue<CharacterConsistencyCheckJob>(
                j => j.ExecuteAsync(projectId, result.Output, userId));
            BackgroundJob.Enqueue<PlotThreadTrackingJob>(
                j => j.ExecuteAsync(projectId, chapterId, userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ChapterDraft] Unexpected error");
            await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "草稿生成发生意外错误");
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
}
