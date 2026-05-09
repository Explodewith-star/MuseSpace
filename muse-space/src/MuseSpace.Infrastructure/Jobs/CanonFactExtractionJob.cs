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
/// Module D 正典事实层 / 固定事实抽取 Job。
/// 由 ChapterDraftJob 草稿落库后异步触发；与 ChapterEventExtractionJob 并行。
/// 把本章里 **状态变化** 写入 canon_facts，新建项目级事实账本。
/// </summary>
public sealed class CanonFactExtractionJob
{
    private const string TaskType = "canon-fact-extract";

    private readonly IAgentRunner _agentRunner;
    private readonly IChapterRepository _chapterRepo;
    private readonly ICanonFactRepository _factRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly AgentSuggestionAppService _suggestionService;
    private readonly IAgentProgressNotifier _progressNotifier;
    private readonly LlmProviderSelector _selector;
    private readonly MuseSpaceDbContext _db;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<CanonFactExtractionJob> _logger;

    public CanonFactExtractionJob(
        IAgentRunner agentRunner,
        IChapterRepository chapterRepo,
        ICanonFactRepository factRepo,
        ICharacterRepository characterRepo,
        AgentSuggestionAppService suggestionService,
        IAgentProgressNotifier progressNotifier,
        LlmProviderSelector selector,
        MuseSpaceDbContext db,
        IFeatureFlagService featureFlags,
        ILogger<CanonFactExtractionJob> logger)
    {
        _agentRunner = agentRunner;
        _chapterRepo = chapterRepo;
        _factRepo = factRepo;
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
        if (!await _featureFlags.IsEnabledAsync(FeatureFlagKeys.AutoCanonFactExtraction, defaultValue: true))
        {
            _logger.LogInformation("[CanonFactExtract] Skipped by feature flag for project {ProjectId}", projectId);
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

            var existingFacts = await _factRepo.GetActiveByOutlineAsync(projectId, chapter.StoryOutlineId);
            var factsText = existingFacts.Count == 0
                ? "（暂无）"
                : string.Join("\n", existingFacts.Select(f =>
                    $"- {f.FactKey} = {f.FactValue} ({f.FactType}, locked={f.IsLocked})"));

            var prompt = $$"""
                ## 项目角色清单（id | 名字）

                {{charsText}}

                ## 当前已记录的活跃事实

                {{factsText}}

                ## 待分析章节正文（第 {{chapter.Number}} 章 {{chapter.Title}}）

                {{chapter.DraftText}}
                """;

            await _progressNotifier.NotifyGeneratingAsync(projectId, TaskType);

            var ctx = new AgentRunContext { UserId = userId, ProjectId = projectId };
            var result = await _agentRunner.RunAsync(
                CanonFactExtractionAgentDefinition.AgentName, prompt, ctx);

            if (!result.Success)
            {
                await _progressNotifier.NotifyFailedAsync(projectId, TaskType,
                    result.ErrorMessage ?? "Agent 执行失败");
                return;
            }

            var output = Internal.LlmJsonExtractor.TryDeserialize<ExtractionOutput>(result.Output);
            if (output is null)
            {
                _logger.LogWarning("[CanonFactExtract] Failed to parse output for chapter {ChapterId}", chapterId);
                await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "无法解析 AI 输出");
                return;
            }

            var nameToId = characters.GroupBy(c => c.Name)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

            int created = 0, updated = 0, invalidated = 0;

            // 1. 新事实：去重写入（已存在同 key → 仅当 value 变化时更新）
            foreach (var f in output.NewFacts ?? new List<NewFactItem>())
            {
                if (string.IsNullOrWhiteSpace(f.FactType) ||
                    string.IsNullOrWhiteSpace(f.FactKey) ||
                    string.IsNullOrWhiteSpace(f.FactValue))
                    continue;

                var existing = await _factRepo.GetByKeyAsync(
                    projectId, chapter.StoryOutlineId, f.FactType!, f.FactKey!);
                if (existing is null)
                {
                    await _factRepo.AddAsync(new CanonFact
                    {
                        StoryProjectId = projectId,
                        StoryOutlineId = chapter.StoryOutlineId,
                        FactType = f.FactType!,
                        SubjectId = LookupId(f.SubjectName, nameToId),
                        ObjectId = LookupId(f.ObjectName, nameToId),
                        FactKey = f.FactKey!,
                        FactValue = f.FactValue!,
                        SourceChapterId = chapterId,
                        Confidence = Math.Clamp(f.Confidence ?? 1.0, 0.0, 1.0),
                        IsLocked = f.IsLocked ?? true,
                        Notes = f.Notes,
                    });
                    created++;
                }
                else if (!string.Equals(existing.FactValue, f.FactValue, StringComparison.Ordinal))
                {
                    existing.FactValue = f.FactValue!;
                    existing.SourceChapterId = chapterId;
                    if (f.IsLocked is not null) existing.IsLocked = f.IsLocked.Value;
                    if (f.Confidence is not null) existing.Confidence = Math.Clamp(f.Confidence.Value, 0.0, 1.0);
                    if (!string.IsNullOrWhiteSpace(f.Notes)) existing.Notes = f.Notes;
                    await _factRepo.UpdateAsync(existing);
                    updated++;
                }
            }

            // 2. 失效：仅对存在且未锁定的事实标记，锁定项需用户手动确认
            foreach (var inv in output.Invalidations ?? new List<InvalidationItem>())
            {
                if (string.IsNullOrWhiteSpace(inv.FactKey)) continue;
                var match = existingFacts.FirstOrDefault(x => x.FactKey == inv.FactKey);
                if (match is null || match.IsLocked) continue;
                match.InvalidatedByChapterId = chapterId;
                if (!string.IsNullOrWhiteSpace(inv.Reason)) match.Notes = inv.Reason;
                await _factRepo.UpdateAsync(match);
                invalidated++;
            }

            if (created + updated + invalidated > 0)
            {
                var contentJson = JsonSerializer.Serialize(new
                {
                    chapterId,
                    created,
                    updated,
                    invalidated,
                    newFacts = output.NewFacts,
                    invalidations = output.Invalidations,
                }, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                });

                await _suggestionService.CreateAsync(
                    agentRunId: ctx.RunId,
                    storyProjectId: projectId,
                    category: SuggestionCategories.CanonFact,
                    title: $"Canon 事实：新增 {created} / 更新 {updated} / 失效 {invalidated}",
                    contentJson: contentJson);
            }

            await _progressNotifier.NotifyDoneAsync(projectId, TaskType,
                $"Canon 事实抽取完成：新增 {created}，更新 {updated}，失效 {invalidated}");
            _logger.LogInformation("[CanonFactExtract] chapter={ChapterId} created={C} updated={U} invalidated={I}",
                chapterId, created, updated, invalidated);

            // 抽取完成后进行 Canon 冲突检测
            BackgroundJob.Enqueue<CanonConflictCheckJob>(
                j => j.ExecuteAsync(projectId, chapterId, userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CanonFactExtract] Unexpected error chapter={ChapterId}", chapterId);
            await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "Canon 事实抽取失败");
        }
    }

    private static Guid? LookupId(string? name, Dictionary<string, Guid> map)
        => string.IsNullOrWhiteSpace(name) ? null
           : map.TryGetValue(name.Trim(), out var id) ? id : null;

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
        public List<NewFactItem>? NewFacts { get; set; }
        public List<InvalidationItem>? Invalidations { get; set; }
    }

    private sealed class NewFactItem
    {
        public string? FactType { get; set; }
        public string? SubjectName { get; set; }
        public string? ObjectName { get; set; }
        public string? FactKey { get; set; }
        public string? FactValue { get; set; }
        public double? Confidence { get; set; }
        public bool? IsLocked { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class InvalidationItem
    {
        public string? FactKey { get; set; }
        public string? Reason { get; set; }
    }
}
