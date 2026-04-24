using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.Story;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/admin/projects")]
[Authorize(Roles = "Admin")]
public class AdminProjectsController(MuseSpaceDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<AdminProjectResponse>>>> GetAll(CancellationToken ct)
    {
        var projects = await db.StoryProjects
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new AdminProjectResponse
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Genre = p.Genre,
                UserId = p.UserId,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<List<AdminProjectResponse>>.Ok(projects));
    }

    [HttpPut("{projectId:guid}/assign")]
    public async Task<ActionResult<ApiResponse<object>>> Assign(
        Guid projectId,
        [FromBody] AssignProjectRequest request,
        CancellationToken ct)
    {
        var project = await db.StoryProjects.FindAsync([projectId], ct);
        if (project is null) return NotFound(ApiResponse<object>.Fail("项目不存在"));

        // userId = null 表示归回游客共享池
        if (request.UserId.HasValue)
        {
            if (!await db.Users.AnyAsync(u => u.Id == request.UserId.Value, ct))
                return BadRequest(ApiResponse<object>.Fail("目标用户不存在"));
        }

        project.UserId = request.UserId;
        project.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpDelete("{projectId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid projectId, CancellationToken ct)
    {
        var project = await db.StoryProjects.FindAsync([projectId], ct);
        if (project is null) return NotFound(ApiResponse<object>.Fail("项目不存在"));

        db.StoryProjects.Remove(project);
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}

public sealed class AdminProjectResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Genre { get; set; }
    public Guid? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AssignProjectRequest
{
    /// <summary>null = 分配到游客共享池</summary>
    public Guid? UserId { get; set; }
}
