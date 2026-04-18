using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Services.Story;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.Story;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class StoryProjectsController : ControllerBase
{
    private readonly StoryProjectAppService _service;

    public StoryProjectsController(StoryProjectAppService service)
        => _service = service;
    /// <summary>
    /// 创建小说项目
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<StoryProjectResponse>>> Create(
        [FromBody] CreateStoryProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<StoryProjectResponse>.Ok(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<StoryProjectResponse>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<List<StoryProjectResponse>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<StoryProjectResponse>>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result is null) return NotFound(ApiResponse<StoryProjectResponse>.Fail("Project not found"));
        return Ok(ApiResponse<StoryProjectResponse>.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(id, cancellationToken);
        if (!deleted) return NotFound(ApiResponse<bool>.Fail("Project not found"));
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
