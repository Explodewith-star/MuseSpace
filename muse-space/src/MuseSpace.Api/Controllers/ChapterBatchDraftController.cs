using Hangfire;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Contracts.Chapters;
using MuseSpace.Contracts.Common;
using MuseSpace.Domain.Entities;
using MuseSpace.Infrastructure.Jobs;
using System.Security.Claims;

namespace MuseSpace.Api.Controllers;

/// <summary>
/// 章节批量草稿生成：创建 + 查询 + 中止。
/// 默认上限 5 章，硬上限 10 章；项目级互斥（同一项目不可并发多个批次）。
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}")]
public class ChapterBatchDraftController : ControllerBase
{
    /// <summary>默认上限。</summary>
    public const int DefaultMaxBatchSize = 5;

    /// <summary>硬上限。</summary>
    public const int HardMaxBatchSize = 10;

    private readonly IChapterBatchDraftRunRepository _runRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly IStoryOutlineRepository _outlineRepo;
    private readonly IBackgroundJobClient _backgroundJobs;

    public ChapterBatchDraftController(
        IChapterBatchDraftRunRepository runRepo,
        IChapterRepository chapterRepo,
        IStoryOutlineRepository outlineRepo,
        IBackgroundJobClient backgroundJobs)
    {
        _runRepo = runRepo;
        _chapterRepo = chapterRepo;
        _outlineRepo = outlineRepo;
        _backgroundJobs = backgroundJobs;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>提交批量生成任务。</summary>
    [HttpPost("chapters/batch-generate-draft")]
    public async Task<ActionResult<ApiResponse<ChapterBatchDraftRunResponse>>> Submit(
        Guid projectId,
        [FromBody] BatchGenerateDraftRequest request,
        CancellationToken ct)
    {
        if (request is null || request.FromNumber <= 0 || request.ToNumber <= 0)
        {
            return BadRequest(ApiResponse<ChapterBatchDraftRunResponse>.Fail("章节号必须为正整数"));
        }

        if (request.FromNumber > request.ToNumber)
        {
            return BadRequest(ApiResponse<ChapterBatchDraftRunResponse>.Fail("起始章节号不能大于结束章节号"));
        }

        var size = request.ToNumber - request.FromNumber + 1;
        if (size > HardMaxBatchSize)
        {
            return BadRequest(ApiResponse<ChapterBatchDraftRunResponse>.Fail(
                $"单批最多 {HardMaxBatchSize} 章"));
        }

        var outline = request.StoryOutlineId.HasValue
            ? await _outlineRepo.GetByIdAsync(projectId, request.StoryOutlineId.Value, ct)
            : await _outlineRepo.GetOrCreateDefaultAsync(projectId, ct);
        if (outline is null)
        {
            return BadRequest(ApiResponse<ChapterBatchDraftRunResponse>.Fail("故事大纲不存在"));
        }

        var allChapters = await _chapterRepo.GetByOutlineAsync(projectId, outline.Id, ct);
        var rangeChapters = allChapters
            .Where(c => c.Number >= request.FromNumber && c.Number <= request.ToNumber)
            .ToList();
        if (rangeChapters.Count == 0)
        {
            return BadRequest(ApiResponse<ChapterBatchDraftRunResponse>.Fail("该范围内不存在章节"));
        }

        // 自动清理卡死（超过 60 分钟仍为 Pending/Running）的历史批次，防止永久阻塞
        await _runRepo.MarkStaleRunsAsFailedAsync(projectId, ct);

        if (await _runRepo.HasActiveAsync(projectId, outline.Id, ct))
        {
            return Conflict(ApiResponse<ChapterBatchDraftRunResponse>.Fail(
                "该大纲已有进行中的批量任务，请等待其完成或先中止"));
        }

        var run = new ChapterBatchDraftRun
        {
            Id = Guid.NewGuid(),
            StoryProjectId = projectId,
            StoryOutlineId = outline.Id,
            UserId = CurrentUserId,
            FromNumber = request.FromNumber,
            ToNumber = request.ToNumber,
            SkipChaptersWithDraft = request.SkipChaptersWithDraft,
            AutoFillPlan = request.AutoFillPlan,
            TotalCount = rangeChapters.Count,
            Status = ChapterBatchDraftStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
        await _runRepo.AddAsync(run, ct);

        _backgroundJobs.Enqueue<BatchChapterDraftJob>(j => j.ExecuteAsync(run.Id));

        return Ok(ApiResponse<ChapterBatchDraftRunResponse>.Ok(Map(run)));
    }

    /// <summary>查询单个批次状态。</summary>
    [HttpGet("chapter-batch-runs/{runId:guid}")]
    public async Task<ActionResult<ApiResponse<ChapterBatchDraftRunResponse>>> Get(
        Guid projectId, Guid runId, CancellationToken ct)
    {
        var run = await _runRepo.GetAsync(projectId, runId, ct);
        if (run is null)
            return NotFound(ApiResponse<ChapterBatchDraftRunResponse>.Fail("批次不存在"));
        return Ok(ApiResponse<ChapterBatchDraftRunResponse>.Ok(Map(run)));
    }

    /// <summary>列出最近批次（默认 10 条）。</summary>
    [HttpGet("chapter-batch-runs")]
    public async Task<ActionResult<ApiResponse<List<ChapterBatchDraftRunResponse>>>> ListRecent(
        Guid projectId,
        [FromQuery] Guid? storyOutlineId,
        [FromQuery] int take = 10,
        CancellationToken ct = default)
    {
        if (take <= 0 || take > 100) take = 10;
        var runs = storyOutlineId.HasValue
            ? await _runRepo.ListRecentAsync(projectId, storyOutlineId.Value, take, ct)
            : await _runRepo.ListRecentAsync(projectId, take, ct);
        return Ok(ApiResponse<List<ChapterBatchDraftRunResponse>>.Ok(
            runs.Select(Map).ToList()));
    }

    /// <summary>请求中止：当前章节完成后停止后续。</summary>
    [HttpPost("chapter-batch-runs/{runId:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<bool>>> Cancel(
        Guid projectId, Guid runId, CancellationToken ct)
    {
        var run = await _runRepo.GetAsync(projectId, runId, ct);
        if (run is null) return NotFound(ApiResponse<bool>.Fail("批次不存在"));
        if (run.Status != ChapterBatchDraftStatus.Pending && run.Status != ChapterBatchDraftStatus.Running)
        {
            return BadRequest(ApiResponse<bool>.Fail("该批次已结束，无需中止"));
        }
        // 立即将 Status 置为 Cancelled，使 HasActiveAsync 不再拦截新任务。
        // Hangfire Job 仍会跑完当前章节，收尾时 reload 到相同状态后正常保存。
        run.CancelRequested = true;
        run.Status = ChapterBatchDraftStatus.Cancelled;
        run.FinishedAt = DateTime.UtcNow;
        await _runRepo.UpdateAsync(run, ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    private static ChapterBatchDraftRunResponse Map(ChapterBatchDraftRun run)
    {
        var dto = run.Adapt<ChapterBatchDraftRunResponse>();
        dto.Status = run.Status.ToString();
        return dto;
    }
}
