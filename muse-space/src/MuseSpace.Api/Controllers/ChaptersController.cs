using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Services.Story;
using MuseSpace.Contracts.Chapters;
using MuseSpace.Contracts.Common;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/chapters")]
public class ChaptersController : ControllerBase
{
    private readonly ChapterAppService _service;

    public ChaptersController(ChapterAppService service)
        => _service = service;

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

    [HttpPut("{chapterId:guid}")]
    public async Task<ActionResult<ApiResponse<ChapterResponse>>> Update(
        Guid projectId, Guid chapterId, [FromBody] UpdateChapterRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(projectId, chapterId, request, cancellationToken);
        if (result is null) return NotFound(ApiResponse<ChapterResponse>.Fail("Chapter not found"));
        return Ok(ApiResponse<ChapterResponse>.Ok(result));
    }
}
