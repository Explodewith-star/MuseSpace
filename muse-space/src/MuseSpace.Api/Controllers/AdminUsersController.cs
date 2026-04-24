using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MuseSpace.Contracts.Auth;
using MuseSpace.Contracts.Common;
using MuseSpace.Domain.Entities;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController(MuseSpaceDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<UserResponse>>>> GetAll(CancellationToken ct)
    {
        var users = await db.Users
            .OrderBy(u => u.CreatedAt)
            .Select(u => new UserResponse
            {
                Id = u.Id,
                PhoneNumber = u.PhoneNumber,
                Role = u.Role,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .ToListAsync(ct);

        return Ok(ApiResponse<List<UserResponse>>.Ok(users));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserResponse>>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken ct)
    {
        var phone = request.PhoneNumber?.Trim();
        if (string.IsNullOrEmpty(phone))
            return BadRequest(ApiResponse<UserResponse>.Fail("手机号不能为空"));

        if (await db.Users.AnyAsync(u => u.PhoneNumber == phone, ct))
            return Conflict(ApiResponse<UserResponse>.Fail("该手机号已存在"));

        var user = new User
        {
            PhoneNumber = phone,
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Ok(ApiResponse<UserResponse>.Ok(new UserResponse
        {
            Id = user.Id,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        }));
    }

    [HttpDelete("{userId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([userId], ct);
        if (user is null) return NotFound(ApiResponse<object>.Fail("用户不存在"));
        if (user.Role == "Admin")
            return BadRequest(ApiResponse<object>.Fail("不能删除管理员账号"));

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
