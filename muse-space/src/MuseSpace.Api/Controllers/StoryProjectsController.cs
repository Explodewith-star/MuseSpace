using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Services.Story;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.Story;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class StoryProjectsController : ControllerBase
{
    private readonly StoryProjectAppService _service;
    private readonly IGenerationRecordRepository _generationRepo;

    public StoryProjectsController(
        StoryProjectAppService service,
        IGenerationRecordRepository generationRepo)
    {
        _service = service;
        _generationRepo = generationRepo;
    }

    /// <summary>解析当前请求的 userId：有 JWT 则取其 NameIdentifier，无 JWT 则返回 null（游客）</summary>
    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpPost]
    public async Task<ActionResult<ApiResponse<StoryProjectResponse>>> Create(
        [FromBody] CreateStoryProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, CurrentUserId, cancellationToken);
        return Ok(ApiResponse<StoryProjectResponse>.Ok(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<StoryProjectResponse>>>> GetAll(CancellationToken cancellationToken)
    {
        // 游客 null → 游客共享项目；登录用户 → 自己的私有项目
        var result = await _service.GetByUserIdAsync(CurrentUserId, cancellationToken);
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

    [HttpGet("{id:guid}/generation-stats")]
    public async Task<ActionResult<ApiResponse<ProjectGenerationStats>>> GetGenerationStats(
        Guid id, CancellationToken cancellationToken)
    {
        var stats = await _generationRepo.GetProjectStatsAsync(id, cancellationToken);
        return Ok(ApiResponse<ProjectGenerationStats>.Ok(stats));
    }
}
