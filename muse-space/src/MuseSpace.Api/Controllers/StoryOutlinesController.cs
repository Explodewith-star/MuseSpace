using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Services.Story;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.Outlines;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/outlines")]
public sealed class StoryOutlinesController : ControllerBase
{
    private readonly StoryOutlineAppService _service;

    public StoryOutlinesController(StoryOutlineAppService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<StoryOutlineResponse>>>> GetAll(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByProjectAsync(projectId, cancellationToken);
        return Ok(ApiResponse<List<StoryOutlineResponse>>.Ok(result));
    }

    [HttpGet("{outlineId:guid}")]
    public async Task<ActionResult<ApiResponse<StoryOutlineResponse>>> GetById(
        Guid projectId,
        Guid outlineId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(projectId, outlineId, cancellationToken);
        if (result is null) return NotFound(ApiResponse<StoryOutlineResponse>.Fail("Outline not found"));
        return Ok(ApiResponse<StoryOutlineResponse>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<StoryOutlineResponse>>> Create(
        Guid projectId,
        [FromBody] CreateStoryOutlineRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(projectId, request, cancellationToken);
        return Ok(ApiResponse<StoryOutlineResponse>.Ok(result));
    }

    [HttpPatch("{outlineId:guid}")]
    public async Task<ActionResult<ApiResponse<StoryOutlineResponse>>> Update(
        Guid projectId,
        Guid outlineId,
        [FromBody] UpdateStoryOutlineRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(projectId, outlineId, request, cancellationToken);
        if (result is null) return NotFound(ApiResponse<StoryOutlineResponse>.Fail("Outline not found"));
        return Ok(ApiResponse<StoryOutlineResponse>.Ok(result));
    }

    [HttpDelete("{outlineId:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid projectId,
        Guid outlineId,
        CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(projectId, outlineId, cancellationToken);
        if (!deleted) return BadRequest(ApiResponse<bool>.Fail("默认大纲不可删除，或大纲不存在"));
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
