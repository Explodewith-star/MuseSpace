using System.Text.Json;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Services.Suggestions;
using MuseSpace.Contracts.Suggestions;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Module D / Canon 冲突守护（确定性，不调用 LLM）。
/// 由 CanonFactExtractionJob 完成后链式触发。检查项：
/// 1. 本章新事件中 EventType 出现在已锁定的 UniqueEvent factKey 列表 → Blocking。
/// 2. 任何已锁定 fact 在本章被 InvalidatedByChapterId 标记 → Blocking（因为锁定项不应被推翻）。
/// </summary>
public sealed class CanonConflictCheckJob
{
    private const string TaskType = "canon-conflict-check";

    private readonly IChapterEventRepository _eventRepo;
    private readonly ICanonFactRepository _factRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly AgentSuggestionAppService _suggestionService;
    private readonly IAgentProgressNotifier _progressNotifier;
    private readonly ILogger<CanonConflictCheckJob> _logger;

    public CanonConflictCheckJob(
        IChapterEventRepository eventRepo,
        ICanonFactRepository factRepo,
        IChapterRepository chapterRepo,
        AgentSuggestionAppService suggestionService,
        IAgentProgressNotifier progressNotifier,
        ILogger<CanonConflictCheckJob> logger)
    {
        _eventRepo = eventRepo;
        _factRepo = factRepo;
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
            var conflicts = new List<CanonConflict>();
            var chapter = await _chapterRepo.GetByIdAsync(projectId, chapterId);
            if (chapter is null)
            {
                await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "章节不存在");
                return;
            }

            // 检查 1：locked UniqueEvent 在本章被重新触发
            var lockedFacts = await _factRepo.GetLockedByOutlineAsync(projectId, chapter.StoryOutlineId);
            var lockedUniqueEventTypes = lockedFacts
                .Where(f => string.Equals(f.FactType, "UniqueEvent", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (lockedUniqueEventTypes.Count > 0)
            {
                var currentEvents = await _eventRepo.GetByChapterAsync(projectId, chapterId);
                foreach (var ev in currentEvents)
                {
                    foreach (var lockedF in lockedUniqueEventTypes)
                    {
                        if (FactKeyMatchesEvent(lockedF.FactKey, ev.EventType))
                        {
                            conflicts.Add(new CanonConflict
                            {
                                Type = "ReTriggeredUniqueEvent",
                                Description = $"已锁定的 UniqueEvent '{lockedF.FactKey}' 在本章再次发生（事件：{ev.EventText}）",
                                FactId = lockedF.Id,
                                FactKey = lockedF.FactKey,
                                EventId = ev.Id,
                                EventText = ev.EventText,
                            });
                        }
                    }
                }
            }

            // 检查 2：本章 invalidated 了 locked fact（不允许）
            var allFacts = await _factRepo.GetByOutlineAsync(projectId, chapter.StoryOutlineId);
            var lockedInvalidatedHere = allFacts
                .Where(f => f.IsLocked && f.InvalidatedByChapterId == chapterId)
                .ToList();
            foreach (var f in lockedInvalidatedHere)
            {
                conflicts.Add(new CanonConflict
                {
                    Type = "LockedFactInvalidated",
                    Description = $"已锁定的事实 '{f.FactKey} = {f.FactValue}' 在本章被标记为失效；锁定项不应被推翻。请人工确认或解锁。",
                    FactId = f.Id,
                    FactKey = f.FactKey,
                });
            }

            if (conflicts.Count > 0)
            {
                var chapterLabel = $"第 {chapter.Number} 章 {chapter.Title}";

                var contentJson = JsonSerializer.Serialize(new
                {
                    chapterId,
                    severity = "Blocking",
                    description = "本章与已锁定的 Canon 事实存在冲突。请回看下列冲突项，决定是修订草稿，还是显式解锁/推翻该事实。",
                    conflicts,
                }, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                });

                await _suggestionService.CreateAsync(
                    agentRunId: Guid.Empty,
                    storyProjectId: projectId,
                    category: SuggestionCategories.CanonFact,
                    title: $"[Blocking] Canon 冲突：{chapterLabel} 共 {conflicts.Count} 处",
                    contentJson: contentJson,
                    targetEntityId: chapterId);
            }

            await _progressNotifier.NotifyDoneAsync(projectId, TaskType,
                conflicts.Count == 0
                    ? "未发现 Canon 冲突"
                    : $"发现 {conflicts.Count} 处 Canon 冲突");
            _logger.LogInformation("[CanonConflictCheck] chapter={ChapterId} conflicts={N}",
                chapterId, conflicts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CanonConflictCheck] Unexpected error chapter={ChapterId}", chapterId);
            await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "Canon 冲突检查失败");
        }
    }

    /// <summary>
    /// FactKey 形如 "UniqueEvent:proposal:A-B"；第二段为事件类型 token，参与匹配。
    /// </summary>
    private static bool FactKeyMatchesEvent(string factKey, string eventType)
    {
        if (string.IsNullOrWhiteSpace(factKey) || string.IsNullOrWhiteSpace(eventType)) return false;
        var parts = factKey.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        return string.Equals(parts[1], eventType, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CanonConflict
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Guid? FactId { get; set; }
        public string? FactKey { get; set; }
        public Guid? EventId { get; set; }
        public string? EventText { get; set; }
    }
}
