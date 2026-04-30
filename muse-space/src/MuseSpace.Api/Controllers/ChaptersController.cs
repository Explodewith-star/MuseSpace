using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Services.Story;
using MuseSpace.Contracts.Chapters;
using MuseSpace.Contracts.Common;
using Hangfire;
using MuseSpace.Infrastructure.Jobs;
using System.Security.Claims;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/chapters")]
public class ChaptersController : ControllerBase
{
    private readonly ChapterAppService _service;
    private readonly IBackgroundJobClient _backgroundJobs;

    public ChaptersController(ChapterAppService service, IBackgroundJobClient backgroundJobs)
    {
        _service = service;
        _backgroundJobs = backgroundJobs;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ChapterResponse>>> Create(
        Guid projectId, [FromBody] CreateChapterRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(projectId, request, cancellationToken);
        return Ok(ApiResponse<ChapterResponse>.Ok(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ChapterResponse>>>> GetAll(
        Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByProjectAsync(projectId, cancellationToken);
        return Ok(ApiResponse<List<ChapterResponse>>.Ok(result));
    }

    [HttpGet("{chapterId:guid}")]
    public async Task<ActionResult<ApiResponse<ChapterResponse>>> GetById(
        Guid projectId, Guid chapterId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(projectId, chapterId, cancellationToken);
        if (result is null) return NotFound(ApiResponse<ChapterResponse>.Fail("Chapter not found"));
        return Ok(ApiResponse<ChapterResponse>.Ok(result));
    }

    [HttpDelete("{chapterId:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid projectId, Guid chapterId, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(projectId, chapterId, cancellationToken);
        if (!deleted) return NotFound(ApiResponse<bool>.Fail("Chapter not found"));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    /// <summary>批量删除章节（级联删除关联 Scene / 草稿 / 定稿）。</summary>
    [HttpPost("batch-delete")]
    public async Task<ActionResult<ApiResponse<int>>> BatchDelete(
        Guid projectId, [FromBody] BatchDeleteChaptersRequest request, CancellationToken cancellationToken)
    {
        var count = await _service.BatchDeleteAsync(projectId, request.ChapterIds, cancellationToken);
        return Ok(ApiResponse<int>.Ok(count));
    }

    /// <summary>
    /// 批量重排章节编号，按请求中 ChapterIds 顺序赋值 Number（默认从 1 起）。
    /// 用于消除删除章节后编号出现空洞的情况。
    /// </summary>
    [HttpPost("batch-reorder")]
    public async Task<ActionResult<ApiResponse<int>>> BatchReorder(
        Guid projectId, [FromBody] BatchReorderChaptersRequest request, CancellationToken cancellationToken)
    {
        var count = await _service.BatchReorderAsync(
            projectId, request.ChapterIds, request.StartNumber, cancellationToken);
        return Ok(ApiResponse<int>.Ok(count));
    }

    [HttpPut("{chapterId:guid}")]
    public async Task<ActionResult<ApiResponse<ChapterResponse>>> Update(
        Guid projectId, Guid chapterId, [FromBody] UpdateChapterRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(projectId, chapterId, request, cancellationToken);
        if (result is null) return NotFound(ApiResponse<ChapterResponse>.Fail("Chapter not found"));
        return Ok(ApiResponse<ChapterResponse>.Ok(result));
    }

    /// <summary>触发本章节自动规划（写回 Conflict/EmotionCurve/KeyCharacterIds/MustIncludePoints）。</summary>
    [HttpPost("{chapterId:guid}/auto-plan")]
    public ActionResult<ApiResponse<string>> AutoPlan(Guid projectId, Guid chapterId)
    {
        var userId = CurrentUserId;
        _backgroundJobs.Enqueue<ChapterAutoPlanJob>(
            job => job.ExecuteAsync(projectId, chapterId, userId));
        return Ok(ApiResponse<string>.Ok("已提交章节自动规划任务"));
    }

    /// <summary>触发本章节草稿生成（基于章节计划字段）。</summary>
    [HttpPost("{chapterId:guid}/generate-draft")]
    public ActionResult<ApiResponse<string>> GenerateDraft(Guid projectId, Guid chapterId)
    {
        var userId = CurrentUserId;
        _backgroundJobs.Enqueue<ChapterDraftJob>(
            job => job.ExecuteAsync(projectId, chapterId, userId));
        return Ok(ApiResponse<string>.Ok("已提交章节草稿生成任务"));
    }
}
