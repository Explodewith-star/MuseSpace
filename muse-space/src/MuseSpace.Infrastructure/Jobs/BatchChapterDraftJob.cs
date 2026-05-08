using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Features;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Hangfire Job：批量生成章节草稿（顺序串行）。
/// - 默认上限 5 章，硬上限 10 章（由 Controller 校验）。
/// - 单章 10 分钟超时，单批 45 分钟整体超时。
/// - 单章失败不中断后续；记录到 FailedChapterIds[]。
/// - CancelRequested 在每章开始前检查；运行中的章节做完后才停止后续。
/// - AutoFillPlan=true 时，每章先调 ChapterAutoPlanJob（如该章计划字段为空），再生成草稿。
/// - 不并发：避免 LLM 速率限制 + 第 N 章生成依赖第 N-1 章已写入。
/// </summary>
public sealed class BatchChapterDraftJob
{
    /// <summary>单章超时（计划+草稿共用）。</summary>
    private static readonly TimeSpan SingleChapterTimeout = TimeSpan.FromMinutes(10);

    /// <summary>整批超时。</summary>
    private static readonly TimeSpan BatchTimeout = TimeSpan.FromMinutes(45);

    private readonly ChapterDraftJob _chapterDraftJob;
    private readonly ChapterAutoPlanJob _autoPlanJob;
    private readonly IFeatureFlagService _featureFlags;
    private readonly IAgentProgressNotifier _progressNotifier;
    private readonly ITaskProgressService _taskProgress;
    private readonly MuseSpaceDbContext _db;
    private readonly ILogger<BatchChapterDraftJob> _logger;

    private const string TaskType = "chapter-batch-draft";

    public BatchChapterDraftJob(
        ChapterDraftJob chapterDraftJob,
        ChapterAutoPlanJob autoPlanJob,
        IFeatureFlagService featureFlags,
        IAgentProgressNotifier progressNotifier,
        ITaskProgressService taskProgress,
        MuseSpaceDbContext db,
        ILogger<BatchChapterDraftJob> logger)
    {
        _chapterDraftJob = chapterDraftJob;
        _autoPlanJob = autoPlanJob;
        _featureFlags = featureFlags;
        _progressNotifier = progressNotifier;
        _taskProgress = taskProgress;
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid runId)
    {
        var run = await _db.ChapterBatchDraftRuns.FirstOrDefaultAsync(r => r.Id == runId);
        if (run is null)
        {
            _logger.LogWarning("[BatchChapterDraft] Run not found: {RunId}", runId);
            return;
        }

        // 早退：Cancel 接口已将状态置为 Cancelled，Job 无需执行
        if (run.CancelRequested)
        {
            run.Status = ChapterBatchDraftStatus.Cancelled;
            run.FinishedAt ??= DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return;
        }

        // 总开关检查
        var enabled = await _featureFlags.IsEnabledAsync(FeatureFlagKeys.BatchDraftEnabled, defaultValue: true);
        if (!enabled)
        {
            run.Status = ChapterBatchDraftStatus.Failed;
            run.ErrorMessage = "批量生成功能已被关闭（FeatureFlag: batch-draft-enabled）";
            run.FinishedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _progressNotifier.NotifyFailedAsync(run.StoryProjectId, TaskType, run.ErrorMessage);
            return;
        }

        run.Status = ChapterBatchDraftStatus.Running;
        run.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _progressNotifier.NotifyStartedAsync(run.StoryProjectId, TaskType);

        var bgTaskId = await _taskProgress.StartAsync(
            run.UserId, run.StoryProjectId, BackgroundTaskType.BatchDraftGeneration,
            $"批量生成草稿（{run.FromNumber}–{run.ToNumber}章）");

        var chapters = await _db.Chapters
            .Where(c => c.StoryProjectId == run.StoryProjectId
                     && c.Number >= run.FromNumber
                     && c.Number <= run.ToNumber)
            .OrderBy(c => c.Number)
            .ToListAsync();

        run.TotalCount = chapters.Count;
        await _db.SaveChangesAsync();

        if (chapters.Count == 0)
        {
            run.Status = ChapterBatchDraftStatus.Failed;
            run.ErrorMessage = "范围内没有章节";
            run.FinishedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _progressNotifier.NotifyFailedAsync(run.StoryProjectId, TaskType, run.ErrorMessage);
            return;
        }

        using var batchCts = new CancellationTokenSource(BatchTimeout);

        try
        {
            foreach (var chapter in chapters)
            {
                // 重新加载 run 以读取最新 CancelRequested
                await _db.Entry(run).ReloadAsync();
                if (run.CancelRequested) break;
                if (batchCts.IsCancellationRequested) break;

                if (run.SkipChaptersWithDraft && !string.IsNullOrWhiteSpace(chapter.DraftText))
                {
                    run.SkippedCount++;
                    await _db.SaveChangesAsync();
                    continue;
                }

                run.CurrentChapterId = chapter.Id;
                await _db.SaveChangesAsync();

                using var singleCts = CancellationTokenSource.CreateLinkedTokenSource(batchCts.Token);
                singleCts.CancelAfter(SingleChapterTimeout);

                try
                {
                    var chapterLabel = $"第 {chapter.Number} 章（{run.CompletedCount + run.FailedCount + run.SkippedCount + 1}/{run.TotalCount}）";

                    // 步骤 1：自动填充写作计划（仅当 AutoFillPlan=true 且该章无计划字段时）
                    if (run.AutoFillPlan)
                    {
                        var needsPlan = string.IsNullOrWhiteSpace(chapter.Conflict)
                            && string.IsNullOrWhiteSpace(chapter.EmotionCurve)
                            && (chapter.KeyCharacterIds is null || chapter.KeyCharacterIds.Count == 0)
                            && (chapter.MustIncludePoints is null || chapter.MustIncludePoints.Count == 0);

                        if (needsPlan)
                        {
                            await _taskProgress.ReportProgressAsync(bgTaskId,
                                CalcPercent(run),
                                $"{chapterLabel}：正在自动填充写作计划…");

                            _logger.LogInformation("[BatchChapterDraft] AutoPlan for chapter {Id}", chapter.Id);
                            await _autoPlanJob.ExecuteAsync(run.StoryProjectId, chapter.Id, run.UserId);

                            // 重新加载章节，确保草稿生成时能读到最新计划字段
                            var planned = await _db.Chapters.AsNoTracking()
                                .FirstOrDefaultAsync(c => c.Id == chapter.Id);
                            if (planned is not null)
                            {
                                chapter.Conflict = planned.Conflict;
                                chapter.EmotionCurve = planned.EmotionCurve;
                                chapter.KeyCharacterIds = planned.KeyCharacterIds;
                                chapter.MustIncludePoints = planned.MustIncludePoints;
                            }
                        }
                        else
                        {
                            _logger.LogInformation("[BatchChapterDraft] Chapter {Id} already has plan, skipping AutoPlan", chapter.Id);
                        }
                    }

                    // 步骤 2：生成草稿
                    await _taskProgress.ReportProgressAsync(bgTaskId,
                        CalcPercent(run),
                        $"{chapterLabel}：正在生成草稿…");

                    // 直接同步调用 ChapterDraftJob，不入队（保证顺序）
                    await _chapterDraftJob.ExecuteAsync(run.StoryProjectId, chapter.Id, run.UserId);

                    // 重新拉取章节判断是否真的写入了 DraftText（ChapterDraftJob 失败时不抛异常，只 NotifyFailed）
                    var refreshed = await _db.Chapters.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == chapter.Id);
                    if (refreshed is not null
                        && (refreshed.Status == ChapterStatus.Drafting || !string.IsNullOrWhiteSpace(refreshed.DraftText)))
                    {
                        run.CompletedCount++;
                    }
                    else
                    {
                        run.FailedCount++;
                        run.FailedChapterIds.Add(chapter.Id);
                    }
                }
                catch (OperationCanceledException) when (singleCts.IsCancellationRequested && !batchCts.IsCancellationRequested)
                {
                    _logger.LogWarning("[BatchChapterDraft] Chapter {ChapterId} timed out", chapter.Id);
                    run.FailedCount++;
                    run.FailedChapterIds.Add(chapter.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[BatchChapterDraft] Chapter {ChapterId} unexpected error", chapter.Id);
                    run.FailedCount++;
                    run.FailedChapterIds.Add(chapter.Id);
                }
                finally
                {
                    await _db.SaveChangesAsync();
                    if (run.TotalCount > 0)
                    {
                        var pct = (run.CompletedCount + run.FailedCount + run.SkippedCount) * 100 / run.TotalCount;
                        await _taskProgress.ReportProgressAsync(bgTaskId, pct,
                            $"已完成 {run.CompletedCount}/{run.TotalCount} 章");
                    }
                }
            }

            // 收尾状态
            await _db.Entry(run).ReloadAsync();
            run.CurrentChapterId = null;
            run.FinishedAt = DateTime.UtcNow;

            if (run.CancelRequested)
            {
                run.Status = ChapterBatchDraftStatus.Cancelled;
            }
            else if (batchCts.IsCancellationRequested)
            {
                run.Status = ChapterBatchDraftStatus.Failed;
                run.ErrorMessage = "整批超时（30 分钟）";
            }
            else if (run.FailedCount > 0 && run.CompletedCount > 0)
            {
                run.Status = ChapterBatchDraftStatus.PartiallyFailed;
            }
            else if (run.FailedCount > 0)
            {
                run.Status = ChapterBatchDraftStatus.Failed;
                run.ErrorMessage ??= "全部章节生成失败";
            }
            else
            {
                run.Status = ChapterBatchDraftStatus.Completed;
            }

            await _db.SaveChangesAsync();

            var summary = run.Status switch
            {
                ChapterBatchDraftStatus.Completed =>
                    $"批量生成完成：{run.CompletedCount}/{run.TotalCount}",
                ChapterBatchDraftStatus.PartiallyFailed =>
                    $"批量生成部分完成：成功 {run.CompletedCount}，失败 {run.FailedCount}",
                ChapterBatchDraftStatus.Cancelled =>
                    $"已中止：成功 {run.CompletedCount}/{run.TotalCount}",
                _ => $"批量生成失败：{run.ErrorMessage}",
            };

            if (run.Status == ChapterBatchDraftStatus.Completed
                || run.Status == ChapterBatchDraftStatus.PartiallyFailed
                || run.Status == ChapterBatchDraftStatus.Cancelled)
            {
                await _progressNotifier.NotifyDoneAsync(run.StoryProjectId, TaskType, summary);
                await _taskProgress.CompleteAsync(bgTaskId, summary);
            }
            else
            {
                await _progressNotifier.NotifyFailedAsync(run.StoryProjectId, TaskType, summary);
                await _taskProgress.FailAsync(bgTaskId, summary);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BatchChapterDraft] Unexpected batch-level error");
            run.Status = ChapterBatchDraftStatus.Failed;
            run.ErrorMessage = ex.Message;
            run.FinishedAt = DateTime.UtcNow;
            run.CurrentChapterId = null;
            await _db.SaveChangesAsync();
            await _progressNotifier.NotifyFailedAsync(run.StoryProjectId, TaskType, "批量生成发生意外错误");
            await _taskProgress.FailAsync(bgTaskId, ex.Message);
        }
    }

    /// <summary>基于当前完成/失败/跳过数计算粗略进度百分比（0-95，保留 100 给完成时）。</summary>
    private static int CalcPercent(ChapterBatchDraftRun run)
    {
        if (run.TotalCount <= 0) return 0;
        var done = run.CompletedCount + run.FailedCount + run.SkippedCount;
        return Math.Min(95, done * 100 / run.TotalCount);
    }
}
