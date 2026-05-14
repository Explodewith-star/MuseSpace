using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Agents;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Services.Agents;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Hangfire Job：对已有大纲的指定章节范围进行局部调整（展开 / 合并）。
/// 接收用户自然语言指令 + 目标章节编号，通过 Agent 输出变更 diff，
/// 写回 Chapter 表并重排编号。
/// </summary>
public sealed class OutlineAdjustJob
{
    private const string TaskType = "outline-adjust";

    private readonly IAgentRunner _agentRunner;
    private readonly IChapterRepository _chapterRepo;
    private readonly IStoryOutlineRepository _outlineRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly IAgentProgressNotifier _progressNotifier;
    private readonly ITaskProgressService _taskProgress;
    private readonly LlmProviderSelector _selector;
    private readonly MuseSpaceDbContext _db;
    private readonly ILogger<OutlineAdjustJob> _logger;

    public OutlineAdjustJob(
        IAgentRunner agentRunner,
        IChapterRepository chapterRepo,
        IStoryOutlineRepository outlineRepo,
        ICharacterRepository characterRepo,
        IAgentProgressNotifier progressNotifier,
        ITaskProgressService taskProgress,
        LlmProviderSelector selector,
        MuseSpaceDbContext db,
        ILogger<OutlineAdjustJob> logger)
    {
        _agentRunner = agentRunner;
        _chapterRepo = chapterRepo;
        _outlineRepo = outlineRepo;
        _characterRepo = characterRepo;
        _progressNotifier = progressNotifier;
        _taskProgress = taskProgress;
        _selector = selector;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 执行大纲章节调整。
    /// </summary>
    /// <param name="projectId">项目 ID。</param>
    /// <param name="outlineId">大纲 ID。</param>
    /// <param name="instruction">用户调整指令（自然语言）。</param>
    /// <param name="targetChapterNumbers">目标章节编号列表。</param>
    /// <param name="targetCount">期望结果章节数（Expand 时使用）。</param>
    /// <param name="userId">触发用户 ID。</param>
    public async Task ExecuteAsync(
        Guid projectId,
        Guid outlineId,
        string instruction,
        List<int> targetChapterNumbers,
        int? targetCount,
        Guid? userId)
    {
        _logger.LogInformation(
            "[OutlineAdjust] Start project={ProjectId} outline={OutlineId} targets=[{Targets}]",
            projectId, outlineId, string.Join(",", targetChapterNumbers));

        await ApplyUserLlmPreferenceAsync(userId);

        var bgTaskId = await _taskProgress.StartAsync(
            userId, projectId, BackgroundTaskType.OutlineAdjust,
            $"AI 大纲调整（{instruction[..Math.Min(instruction.Length, 20)]}…）");

        await _progressNotifier.NotifyStartedAsync(projectId, TaskType);

        try
        {
            // 1. 加载大纲和所有章节
            var outline = await _outlineRepo.GetByIdAsync(projectId, outlineId);
            if (outline is null)
            {
                await Fail(bgTaskId, projectId, "大纲不存在");
                return;
            }

            var allChapters = await _chapterRepo.GetByOutlineAsync(projectId, outlineId);
            var orderedChapters = allChapters.OrderBy(c => c.Number).ToList();

            if (orderedChapters.Count == 0)
            {
                await Fail(bgTaskId, projectId, "大纲下没有章节");
                return;
            }

            // 找出目标章节
            var targetSet = targetChapterNumbers.ToHashSet();
            var targetChapters = orderedChapters.Where(c => targetSet.Contains(c.Number)).ToList();
            if (targetChapters.Count == 0)
            {
                await Fail(bgTaskId, projectId, "指定的目标章节不存在");
                return;
            }

            await _taskProgress.ReportProgressAsync(bgTaskId, 20, "正在构建上下文…");

            // 2. 组装 prompt：提供完整大纲 + 目标范围 + 前后文
            var prevChapter = orderedChapters.LastOrDefault(c => c.Number < targetChapters.Min(t => t.Number));
            var nextChapter = orderedChapters.FirstOrDefault(c => c.Number > targetChapters.Max(t => t.Number));

            var allChaptersText = string.Join("\n", orderedChapters.Select(c =>
                $"- 第{c.Number}章 《{c.Title ?? "无标题"}》: {c.Summary ?? c.Goal ?? "无摘要"}"));

            var targetText = string.Join("\n", targetChapters.Select(c =>
                $"- 第{c.Number}章 《{c.Title ?? "无标题"}》: goal={c.Goal ?? "无"}, summary={c.Summary ?? "无"}"));

            var prevContext = prevChapter is not null
                ? $"前一章（第{prevChapter.Number}章 《{prevChapter.Title}》）：{prevChapter.Summary ?? prevChapter.Goal ?? "无"}"
                : "（目标章节是第一章，无前文）";

            var nextContext = nextChapter is not null
                ? $"后一章（第{nextChapter.Number}章 《{nextChapter.Title}》）：{nextChapter.Summary ?? nextChapter.Goal ?? "无"}"
                : "（目标章节是最后章，无后文）";

            var targetCountHint = targetCount.HasValue
                ? $"期望调整后的章节数量：{targetCount.Value} 章"
                : "（合并模式，无具体目标数量）";

            var userPrompt = $"""
                ## 完整大纲（{orderedChapters.Count} 章）

                {allChaptersText}

                ## 调整目标章节（{targetChapters.Count} 章）

                {targetText}

                ## 前后文参考

                {prevContext}
                {nextContext}

                ## 调整要求

                用户指令：{instruction}
                {targetCountHint}

                请对目标章节进行调整，保持与前后章的情节连贯。
                """;

            await _progressNotifier.NotifyGeneratingAsync(projectId, TaskType);
            await _taskProgress.ReportProgressAsync(bgTaskId, 40, "AI 正在生成调整方案…");

            // 3. 调用 Agent
            var ctx = new AgentRunContext { UserId = userId, ProjectId = projectId };
            var result = await _agentRunner.RunAsync(
                OutlineAdjustAgentDefinition.AgentName, userPrompt, ctx);

            if (!result.Success)
            {
                await Fail(bgTaskId, projectId, result.ErrorMessage ?? "Agent 执行失败");
                return;
            }

            // 4. 解析 diff
            var json = result.Output.Trim();
            if (json.StartsWith("```"))
                json = Regex.Replace(json, @"```\w*\n?", "").Trim('`').Trim();

            AdjustDiff? diff;
            try
            {
                diff = JsonSerializer.Deserialize<AdjustDiff>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[OutlineAdjust] JSON parse failed: {Json}", json[..Math.Min(json.Length, 200)]);
                await Fail(bgTaskId, projectId, "Agent 返回格式无法解析");
                return;
            }

            if (diff is null || (diff.DeleteNumbers.Count == 0 && diff.InsertChapters.Count == 0))
            {
                await Fail(bgTaskId, projectId, "Agent 未返回有效调整结果");
                return;
            }

            await _taskProgress.ReportProgressAsync(bgTaskId, 70, "正在应用调整结果…");

            // 5. 应用 diff：先删除目标章节，再插入新章节，最后重排编号
            var deleteIds = orderedChapters
                .Where(c => diff.DeleteNumbers.Contains(c.Number))
                .Select(c => c.Id)
                .ToList();

            if (deleteIds.Count > 0)
                await _chapterRepo.BatchDeleteAsync(projectId, deleteIds);

            // 找出插入位置（在删除范围的最小编号之前插入）
            var insertAfterNumber = prevChapter?.Number ?? 0;

            // 按顺序保存新章节，记录 ID 列表以保留 InsertChapters 的原始顺序
            // 使用唯一负数作为临时 Number，避免与已有章节的唯一约束冲突
            var newChapterIds = new List<Guid>();
            var tempNumber = -1;
            foreach (var ch in diff.InsertChapters)
            {
                var newChapter = new Chapter
                {
                    Id = Guid.NewGuid(),
                    StoryProjectId = projectId,
                    StoryOutlineId = outlineId,
                    Number = tempNumber--, // 每个新章节使用不同的负数
                    Title = ch.Title,
                    Goal = ch.Goal,
                    Summary = ch.Summary,
                    Status = ChapterStatus.Planned,
                    KeyCharacterIds = new List<Guid>(),
                    MustIncludePoints = new List<string>(),
                };
                await _chapterRepo.SaveAsync(projectId, newChapter);
                newChapterIds.Add(newChapter.Id);
            }

            // 6. 显式构建最终顺序：
            //    [insertAfterNumber 之前的章节] + [新章节按 InsertChapters 顺序] + [insertAfterNumber 之后的章节]
            //    不依赖 Number==0 的排序技巧，避免 DB 顺序不确定导致的章节编号错乱或第0章残留
            var allAfterDelete = await _chapterRepo.GetByOutlineAsync(projectId, outlineId);
            var newIdSet = newChapterIds.ToHashSet();

            var existingRemaining = allAfterDelete
                .Where(c => !newIdSet.Contains(c.Id))
                .OrderBy(c => c.Number)
                .ToList();

            var beforeInsert = existingRemaining
                .Where(c => c.Number <= insertAfterNumber)
                .ToList();
            var afterInsert = existingRemaining
                .Where(c => c.Number > insertAfterNumber)
                .ToList();

            var orderedIds = beforeInsert.Select(c => c.Id)
                .Concat(newChapterIds)          // 保留 Agent 返回的章节顺序
                .Concat(afterInsert.Select(c => c.Id))
                .ToList();

            await _chapterRepo.BatchReorderAsync(projectId, outlineId, orderedIds, 1);

            _logger.LogInformation("[OutlineAdjust] Done: deleted={Del}, inserted={Ins}",
                deleteIds.Count, diff.InsertChapters.Count);

            await _progressNotifier.NotifyDoneAsync(projectId, TaskType,
                $"大纲已调整：删除 {deleteIds.Count} 章，新增 {diff.InsertChapters.Count} 章");
            await _taskProgress.CompleteAsync(bgTaskId,
                $"大纲调整完成，删除 {deleteIds.Count} 章，新增 {diff.InsertChapters.Count} 章");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OutlineAdjust] Unexpected error");
            await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "大纲调整过程发生意外错误");
            await _taskProgress.FailAsync(bgTaskId, ex.Message);
        }
    }

    private async Task Fail(Guid bgTaskId, Guid projectId, string message)
    {
        await _progressNotifier.NotifyFailedAsync(projectId, TaskType, message);
        await _taskProgress.FailAsync(bgTaskId, message);
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

    private sealed class AdjustDiff
    {
        public List<int> DeleteNumbers { get; set; } = new();
        public List<ChapterItem> InsertChapters { get; set; } = new();
    }

    private sealed class ChapterItem
    {
        public int Number { get; set; }
        public string? Title { get; set; }
        public string? Goal { get; set; }
        public string? Summary { get; set; }
    }
}
