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
/// - 单章 5 分钟超时，单批 30 分钟整体超时。
/// - 单章失败不中断后续；记录到 FailedChapterIds[]。
/// - CancelRequested 在每章开始前检查；运行中的章节做完后才停止后续。
/// - 不并发：避免 LLM 速率限制 + 第 N 章生成依赖第 N-1 章已写入。
/// </summary>
public sealed class BatchChapterDraftJob
{
    /// <summary>单章超时。</summary>
    private static readonly TimeSpan SingleChapterTimeout = TimeSpan.FromMinutes(5);

    /// <summary>整批超时。</summary>
    private static readonly TimeSpan BatchTimeout = TimeSpan.FromMinutes(30);

    private readonly ChapterDraftJob _chapterDraftJob;
    private readonly IFeatureFlagService _featureFlags;
    private readonly IAgentProgressNotifier _progressNotifier;
    private readonly MuseSpaceDbContext _db;
    private readonly ILogger<BatchChapterDraftJob> _logger;

    private const string TaskType = "chapter-batch-draft";

    public BatchChapterDraftJob(
        ChapterDraftJob chapterDraftJob,
        IFeatureFlagService featureFlags,
        IAgentProgressNotifier progressNotifier,
        MuseSpaceDbContext db,
        ILogger<BatchChapterDraftJob> logger)
    {
        _chapterDraftJob = chapterDraftJob;
        _featureFlags = featureFlags;
        _progressNotifier = progressNotifier;
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
                    // 直接同步调用 ChapterDraftJob，不入队（保证顺序）
                    // 子 Job 内部已自行触发后处理链（StyleCC / CharacterCC / PlotThread），异步不阻塞
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
            }
            else
            {
                await _progressNotifier.NotifyFailedAsync(run.StoryProjectId, TaskType, summary);
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
        }
    }
}
