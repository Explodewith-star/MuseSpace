using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Agents;
using MuseSpace.Application.Abstractions.Features;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Services.Agents;
using MuseSpace.Application.Services.Suggestions;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// 伏笔追踪 Hangfire Job（D4-C）。
/// 由章节草稿生成完成后异步触发，亦支持手动 plot-thread-scan。
/// 调用 PlotThreadTrackingAgent，自动写入新线索 + 更新已有线索 + 一条 PlotThread 类目通知建议。
/// </summary>
public sealed class PlotThreadTrackingJob
{
    private const string TaskType = "plot-thread-tracking";

    private readonly IAgentRunner _agentRunner;
    private readonly IPlotThreadRepository _threadRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly AgentSuggestionAppService _suggestionService;
    private readonly IAgentProgressNotifier _progressNotifier;
    private readonly LlmProviderSelector _selector;
    private readonly MuseSpaceDbContext _db;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<PlotThreadTrackingJob> _logger;

    public PlotThreadTrackingJob(
        IAgentRunner agentRunner,
        IPlotThreadRepository threadRepo,
        IChapterRepository chapterRepo,
        AgentSuggestionAppService suggestionService,
        IAgentProgressNotifier progressNotifier,
        LlmProviderSelector selector,
        MuseSpaceDbContext db,
        IFeatureFlagService featureFlags,
        ILogger<PlotThreadTrackingJob> logger)
    {
        _agentRunner = agentRunner;
        _threadRepo = threadRepo;
        _chapterRepo = chapterRepo;
        _suggestionService = suggestionService;
        _progressNotifier = progressNotifier;
        _selector = selector;
        _db = db;
        _featureFlags = featureFlags;
        _logger = logger;
    }

    /// <summary>
    /// 扫描指定章节草稿，更新 PlotThread；chapterId=null 时扫描项目全部草稿（取最近 10 章）。
    /// </summary>
    public async Task ExecuteAsync(Guid projectId, Guid? chapterId, Guid? userId)
    {
        if (!await _featureFlags.IsEnabledAsync(FeatureFlagKeys.AutoPlotThreadTracking, defaultValue: true))
        {
            _logger.LogInformation("[PlotThreadTracking] Skipped by feature flag for project {ProjectId}", projectId);
            return;
        }
        await ApplyUserLlmPreferenceAsync(userId);
        await _progressNotifier.NotifyStartedAsync(projectId, TaskType);

        try
        {
            // 1. 收集草稿
            string draftText;
            Guid? plantedAnchor = chapterId;
            if (chapterId is not null)
            {
                var ch = await _chapterRepo.GetByIdAsync(projectId, chapterId.Value);
                if (ch is null || string.IsNullOrWhiteSpace(ch.DraftText))
                {
                    await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "章节不存在或暂无草稿");
                    return;
                }
                draftText = $"【第{ch.Number}章 {ch.Title}】\n{ch.DraftText}";
            }
            else
            {
                var all = (await _chapterRepo.GetByProjectAsync(projectId))
                    .Where(c => !string.IsNullOrWhiteSpace(c.DraftText))
                    .OrderByDescending(c => c.Number).Take(10).Reverse().ToList();
                if (all.Count == 0)
                {
                    await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "项目下没有任何草稿");
                    return;
                }
                draftText = string.Join("\n\n", all.Select(c =>
                    $"【第{c.Number}章 {c.Title}】\n{c.DraftText}"));
            }

            // 2. 当前线索清单
            var threads = await _threadRepo.GetByProjectAsync(projectId);
            var threadsText = threads.Count == 0
                ? "（暂无）"
                : string.Join("\n", threads.Select(t =>
                    $"- {t.Id} | {t.Title}（{t.Status}, {t.Importance ?? "Medium"}）：{t.Description ?? ""}"));

            var prompt = $$"""
                ## 当前已记录线索

                {{threadsText}}

                ## 待分析草稿

                {{draftText}}
                """;

            await _progressNotifier.NotifyGeneratingAsync(projectId, TaskType);

            var ctx = new AgentRunContext { UserId = userId, ProjectId = projectId };
            var result = await _agentRunner.RunAsync(
                PlotThreadTrackingAgentDefinition.AgentName, prompt, ctx);

            if (!result.Success)
            {
                await _progressNotifier.NotifyFailedAsync(projectId, TaskType,
                    result.ErrorMessage ?? "Agent 执行失败");
                return;
            }

            var json = result.Output;

            var output = Internal.LlmJsonExtractor.TryDeserialize<TrackingOutput>(json);

            if (output is null)
            {
                _logger.LogWarning("[PlotThreadTracking] Failed to parse output for project {ProjectId}", projectId);
                await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "无法解析 AI 输出");
                return;
            }

            int created = 0, updated = 0;

            // 3. 写入新线索
            foreach (var n in output.NewThreads ?? [])
            {
                if (string.IsNullOrWhiteSpace(n.Title)) continue;
                await _threadRepo.AddAsync(new PlotThread
                {
                    StoryProjectId = projectId,
                    Title = n.Title!,
                    Description = n.Description,
                    Importance = string.IsNullOrWhiteSpace(n.Importance) ? "Medium" : n.Importance,
                    Status = ForeshadowingStatus.Introduced,
                    PlantedInChapterId = plantedAnchor,
                });
                created++;
            }

            // 4. 更新已有线索
            foreach (var u in output.Updates ?? [])
            {
                if (u.Id == Guid.Empty) continue;
                var item = threads.FirstOrDefault(t => t.Id == u.Id);
                if (item is null) continue;
                if (Enum.TryParse<ForeshadowingStatus>(u.NewStatus, true, out var ns))
                {
                    item.Status = ns;
                    if (ns == ForeshadowingStatus.PaidOff && plantedAnchor is not null)
                        item.ResolvedInChapterId = plantedAnchor;
                    await _threadRepo.UpdateAsync(item);
                    updated++;
                }
            }

            // 5. 写一条通知建议
            if (created + updated > 0 || !string.IsNullOrWhiteSpace(output.Notes))
            {
                var contentJson = JsonSerializer.Serialize(new
                {
                    chapterId,
                    created,
                    updated,
                    notes = output.Notes,
                    output.NewThreads,
                    output.Updates,
                }, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                });

                await _suggestionService.CreateAsync(
                    agentRunId: ctx.RunId,
                    storyProjectId: projectId,
                    category: SuggestionCategories.PlotThread,
                    title: $"伏笔追踪：新埋 {created} 条 / 更新 {updated} 条",
                    contentJson: contentJson);
            }

            await _progressNotifier.NotifyDoneAsync(projectId, TaskType,
                $"伏笔追踪完成：新增 {created}，更新 {updated}");
            _logger.LogInformation("[PlotThreadTracking] project={ProjectId} created={C} updated={U}",
                projectId, created, updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PlotThreadTracking] Unexpected error project={ProjectId}", projectId);
            await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "伏笔追踪失败");
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

    private sealed class TrackingOutput
    {
        public List<NewThreadItem>? NewThreads { get; set; }
        public List<UpdateItem>? Updates { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class NewThreadItem
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Importance { get; set; }
    }

    private sealed class UpdateItem
    {
        public Guid Id { get; set; }
        public string? NewStatus { get; set; }
        public string? Reason { get; set; }
    }
}
