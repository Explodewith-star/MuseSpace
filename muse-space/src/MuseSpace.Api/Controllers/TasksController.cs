using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.Tasks;
using MuseSpace.Domain.Entities;
using System.Security.Claims;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public sealed class TasksController : ControllerBase
{
    private readonly IBackgroundTaskRepository _repo;

    public TasksController(IBackgroundTaskRepository repo) => _repo = repo;

    /// <summary>获取当前用户最近的后台任务列表</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<BackgroundTaskResponse>>>> List(
        [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(ApiResponse<List<BackgroundTaskResponse>>.Fail("未认证"));

        var records = await _repo.GetByUserAsync(userId.Value, limit, ct);
        var result = records.Select(ToResponse).ToList();
        return Ok(ApiResponse<List<BackgroundTaskResponse>>.Ok(result));
    }

    /// <summary>获取当前用户进行中的任务</summary>
    [HttpGet("active")]
    public async Task<ActionResult<ApiResponse<List<BackgroundTaskResponse>>>> Active(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(ApiResponse<List<BackgroundTaskResponse>>.Fail("未认证"));

        var records = await _repo.GetActiveByUserAsync(userId.Value, ct);
        var result = records.Select(ToResponse).ToList();
        return Ok(ApiResponse<List<BackgroundTaskResponse>>.Ok(result));
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private static BackgroundTaskResponse ToResponse(BackgroundTaskRecord r) => new()
    {
        Id = r.Id,
        TaskType = r.TaskType.ToString(),
        Status = r.Status.ToString(),
        Progress = r.Progress,
        Title = r.Title,
        StatusMessage = r.StatusMessage,
        ErrorMessage = r.ErrorMessage,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };
}
