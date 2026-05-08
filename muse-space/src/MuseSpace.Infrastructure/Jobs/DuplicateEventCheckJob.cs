using System.Text.Json;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Services.Suggestions;
using MuseSpace.Contracts.Suggestions;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Module D / 重复事件守护（确定性，不调用 LLM）。
/// 在 ChapterEventExtractionJob 完成后被链式触发：
/// 比对本章新抽取事件 与 历史已标记 IsIrreversible 的事件；若 EventType 重复且参与角色重叠 → 产出 Blocking 建议。
/// </summary>
public sealed class DuplicateEventCheckJob
{
    private const string TaskType = "duplicate-event-check";

    private readonly IChapterEventRepository _eventRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly AgentSuggestionAppService _suggestionService;
    private readonly IAgentProgressNotifier _progressNotifier;
    private readonly ILogger<DuplicateEventCheckJob> _logger;

    public DuplicateEventCheckJob(
        IChapterEventRepository eventRepo,
        IChapterRepository chapterRepo,
        AgentSuggestionAppService suggestionService,
        IAgentProgressNotifier progressNotifier,
        ILogger<DuplicateEventCheckJob> logger)
    {
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
        _suggestionService = suggestionService;
        _progressNotifier = progressNotifier;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid projectId, Guid chapterId, Guid? userId)
    {
        await _progressNotifier.NotifyStartedAsync(projectId, TaskType);

        try
        {
            var currentEvents = await _eventRepo.GetByChapterAsync(projectId, chapterId);
            if (currentEvents.Count == 0)
            {
                await _progressNotifier.NotifyDoneAsync(projectId, TaskType, "本章无事件，跳过重复检测");
                return;
            }

            // 项目内全部不可逆事件（含本章本身）
            var irreversibleAll = await _eventRepo.GetIrreversibleAsync(projectId);
            // 排除本章自身的不可逆事件
            var historicalIrreversible = irreversibleAll
                .Where(e => e.ChapterId != chapterId)
                .ToList();

            if (historicalIrreversible.Count == 0)
            {
                await _progressNotifier.NotifyDoneAsync(projectId, TaskType, "项目暂无历史不可逆事件，跳过");
                return;
            }

            var conflicts = new List<DuplicateConflict>();
            foreach (var cur in currentEvents)
            {
                foreach (var past in historicalIrreversible.Where(p =>
                    string.Equals(p.EventType, cur.EventType, StringComparison.OrdinalIgnoreCase)))
                {
                    // 角色重叠判断：actor 或 target 任一交集
                    if (HasParticipantOverlap(cur, past))
                    {
                        conflicts.Add(new DuplicateConflict
                        {
                            CurrentEventId = cur.Id,
                            CurrentText = cur.EventText,
                            EventType = cur.EventType,
                            PastChapterId = past.ChapterId,
                            PastEventId = past.Id,
                            PastText = past.EventText,
                        });
                    }
                }
            }

            if (conflicts.Count > 0)
            {
                var chapter = await _chapterRepo.GetByIdAsync(projectId, chapterId);
                var chapterLabel = chapter is null ? "未知章节" : $"第 {chapter.Number} 章 {chapter.Title}";

                var contentJson = JsonSerializer.Serialize(new
                {
                    chapterId,
                    severity = "Blocking",
                    type = "DuplicateIrreversibleEvent",
                    description = "本章重新发生了已被标记为不可重复的历史事件。请回看历史章节，调整本章情节避免重复。",
                    conflicts,
                }, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                });

                await _suggestionService.CreateAsync(
                    agentRunId: Guid.Empty,
                    storyProjectId: projectId,
                    category: SuggestionCategories.CanonEvent,
                    title: $"[Blocking] 重复事件：{chapterLabel} 共 {conflicts.Count} 处",
                    contentJson: contentJson,
                    targetEntityId: chapterId);
            }

            await _progressNotifier.NotifyDoneAsync(projectId, TaskType,
                conflicts.Count == 0
                    ? "未发现重复不可逆事件"
                    : $"发现 {conflicts.Count} 处重复不可逆事件");
            _logger.LogInformation("[DuplicateEventCheck] chapter={ChapterId} conflicts={N}",
                chapterId, conflicts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DuplicateEventCheck] Unexpected error chapter={ChapterId}", chapterId);
            await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "重复事件检查失败");
        }
    }

    private static bool HasParticipantOverlap(Domain.Entities.ChapterEvent a, Domain.Entities.ChapterEvent b)
    {
        // 若双方都未指定参与者，仅靠 EventType 匹配；为减少误报，这种情况只在 actorIds 同时为空且 type 完全一致时算重复
        var aIds = (a.ActorCharacterIds ?? new List<Guid>()).Concat(a.TargetCharacterIds ?? new List<Guid>()).ToHashSet();
        var bIds = (b.ActorCharacterIds ?? new List<Guid>()).Concat(b.TargetCharacterIds ?? new List<Guid>()).ToHashSet();
        if (aIds.Count == 0 && bIds.Count == 0) return true;
        return aIds.Overlaps(bIds);
    }

    private sealed class DuplicateConflict
    {
        public Guid CurrentEventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string CurrentText { get; set; } = string.Empty;
        public Guid PastChapterId { get; set; }
        public Guid PastEventId { get; set; }
        public string PastText { get; set; } = string.Empty;
    }
}
