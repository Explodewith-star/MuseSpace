using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MuseSpace.Contracts.Common;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Api.Authorization;

/// <summary>
/// 校验路由参数 <c>projectId</c> 指向的项目归属于当前用户（或当前用户为游客时项目也是游客项目）。
/// 用法：在 Controller / Action 上标注 <c>[ProjectAccess]</c>。
/// 路由必须包含 <c>{projectId:guid}</c> 段。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ProjectAccessAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!context.RouteData.Values.TryGetValue("projectId", out var raw) ||
            raw is null || !Guid.TryParse(raw.ToString(), out var projectId))
        {
            context.Result = new BadRequestObjectResult(
                ApiResponse<object>.Fail("缺少 projectId 路由参数"));
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<MuseSpaceDbContext>();
        var ownerId = await db.StoryProjects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => (Guid?)p.UserId)
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        // 项目不存在 → 404 而非 403（避免泄露存在性，但保持现有 API 行为一致）
        if (ownerId is null && !await db.StoryProjects.AsNoTracking().AnyAsync(p => p.Id == projectId,
                context.HttpContext.RequestAborted))
        {
            context.Result = new NotFoundObjectResult(ApiResponse<object>.Fail("项目不存在"));
            return;
        }

        Guid? currentUserId = Guid.TryParse(
            context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var u) ? u : null;

        // 登录用户访问别人的项目 → 403；游客访问个人项目 → 403。
        // 双方都为 null（游客访问游客项目）允许通过。
        if (ownerId != currentUserId)
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail("无权访问该项目"))
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }
    }
}
