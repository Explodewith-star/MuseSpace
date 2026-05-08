using System.Text.Json;
using Hangfire;
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
/// Module D 正典事实层 / 章节事件抽取 Job。
/// 由 ChapterDraftJob 草稿落库后异步触发，调用 ChapterEventExtractionAgent，
/// 将抽取出的事件批量替换到 chapter_events 表，并产出一条 CanonEvent 类目通知建议。
/// </summary>
public sealed class ChapterEventExtractionJob
{
    private const string TaskType = "chapter-event-extract";

    private readonly IAgentRunner _agentRunner;
    private readonly IChapterRepository _chapterRepo;
    private readonly IChapterEventRepository _eventRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly AgentSuggestionAppService _suggestionService;
    private readonly IAgentProgressNotifier _progressNotifier;
    private readonly LlmProviderSelector _selector;
    private readonly MuseSpaceDbContext _db;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<ChapterEventExtractionJob> _logger;

    public ChapterEventExtractionJob(
        IAgentRunner agentRunner,
        IChapterRepository chapterRepo,
        IChapterEventRepository eventRepo,
        ICharacterRepository characterRepo,
        AgentSuggestionAppService suggestionService,
        IAgentProgressNotifier progressNotifier,
        LlmProviderSelector selector,
        MuseSpaceDbContext db,
        IFeatureFlagService featureFlags,
        ILogger<ChapterEventExtractionJob> logger)
    {
        _agentRunner = agentRunner;
        _chapterRepo = chapterRepo;
        _eventRepo = eventRepo;
        _characterRepo = characterRepo;
        _suggestionService = suggestionService;
        _progressNotifier = progressNotifier;
        _selector = selector;
        _db = db;
        _featureFlags = featureFlags;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid projectId, Guid chapterId, Guid? userId)
    {
        if (!await _featureFlags.IsEnabledAsync(FeatureFlagKeys.AutoChapterEventExtraction, defaultValue: true))
        {
            _logger.LogInformation("[ChapterEventExtract] Skipped by feature flag for project {ProjectId}", projectId);
            return;
        }
        await ApplyUserLlmPreferenceAsync(userId);
        await _progressNotifier.NotifyStartedAsync(projectId, TaskType);

        try
        {
            var chapter = await _chapterRepo.GetByIdAsync(projectId, chapterId);
            if (chapter is null || string.IsNullOrWhiteSpace(chapter.DraftText))
            {
                await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "章节不存在或暂无草稿");
                return;
            }

            var characters = await _characterRepo.GetByProjectAsync(projectId);
            var charsText = characters.Count == 0
                ? "（暂无角色）"
                : string.Join("\n", characters.Select(c => $"- {c.Id} | {c.Name}"));

            var prompt = $$"""
                ## 项目角色清单（id | 名字）

                {{charsText}}

                ## 待分析章节正文（第 {{chapter.Number}} 章 {{chapter.Title}}）

                {{chapter.DraftText}}
                """;

            await _progressNotifier.NotifyGeneratingAsync(projectId, TaskType);

            var ctx = new AgentRunContext { UserId = userId, ProjectId = projectId };
            var result = await _agentRunner.RunAsync(
                ChapterEventExtractionAgentDefinition.AgentName, prompt, ctx);

            if (!result.Success)
            {
                await _progressNotifier.NotifyFailedAsync(projectId, TaskType,
                    result.ErrorMessage ?? "Agent 执行失败");
                return;
            }

            var output = Internal.LlmJsonExtractor.TryDeserialize<ExtractionOutput>(result.Output);
            if (output is null)
            {
                _logger.LogWarning("[ChapterEventExtract] Failed to parse output for chapter {ChapterId}", chapterId);
                await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "无法解析 AI 输出");
                return;
            }

            // 角色名 -> Id 映射（大小写敏感名匹配，找不到的角色名忽略）
            var nameToId = characters.GroupBy(c => c.Name)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

            var entities = (output.Events ?? new List<EventItem>())
                .Where(e => !string.IsNullOrWhiteSpace(e.EventType) && !string.IsNullOrWhiteSpace(e.EventText))
                .Select((e, idx) => new ChapterEvent
                {
                    StoryProjectId = projectId,
                    ChapterId = chapterId,
                    Order = e.Order > 0 ? e.Order : idx + 1,
                    EventType = e.EventType!.Trim(),
                    EventText = e.EventText!.Trim(),
                    ActorCharacterIds = ResolveIds(e.ActorNames, nameToId),
                    TargetCharacterIds = ResolveIds(e.TargetNames, nameToId),
                    Location = e.Location,
                    TimePoint = e.TimePoint,
                    Importance = string.IsNullOrWhiteSpace(e.Importance) ? "Medium" : e.Importance,
                    IsIrreversible = e.IsIrreversible,
                })
                .ToList();

            await _eventRepo.ReplaceForChapterAsync(projectId, chapterId, entities);

            if (entities.Count > 0)
            {
                var contentJson = JsonSerializer.Serialize(new
                {
                    chapterId,
                    extracted = entities.Count,
                    events = entities.Select(e => new
                    {
                        e.EventType,
                        e.EventText,
                        e.IsIrreversible,
                        e.Importance,
                    }),
                }, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                });

                await _suggestionService.CreateAsync(
                    agentRunId: ctx.RunId,
                    storyProjectId: projectId,
                    category: SuggestionCategories.CanonEvent,
                    title: $"事件抽取：第 {chapter.Number} 章 - {entities.Count} 条",
                    contentJson: contentJson);
            }

            await _progressNotifier.NotifyDoneAsync(projectId, TaskType,
                $"事件抽取完成：第 {chapter.Number} 章共 {entities.Count} 条");
            _logger.LogInformation("[ChapterEventExtract] chapter={ChapterId} extracted={N}", chapterId, entities.Count);

            // 抽取完成后进行重复事件检测
            BackgroundJob.Enqueue<DuplicateEventCheckJob>(
                j => j.ExecuteAsync(projectId, chapterId, userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ChapterEventExtract] Unexpected error chapter={ChapterId}", chapterId);
            await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "事件抽取失败");
        }
    }

    private static List<Guid>? ResolveIds(List<string>? names, Dictionary<string, Guid> map)
    {
        if (names is null || names.Count == 0) return null;
        var ids = names.Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => map.TryGetValue(n.Trim(), out var id) ? (Guid?)id : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        return ids.Count == 0 ? null : ids;
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

    private sealed class ExtractionOutput
    {
        public List<EventItem>? Events { get; set; }
    }

    private sealed class EventItem
    {
        public string? EventType { get; set; }
        public string? EventText { get; set; }
        public List<string>? ActorNames { get; set; }
        public List<string>? TargetNames { get; set; }
        public string? Location { get; set; }
        public string? TimePoint { get; set; }
        public string? Importance { get; set; }
        public bool IsIrreversible { get; set; }
        public int Order { get; set; }
    }
}
