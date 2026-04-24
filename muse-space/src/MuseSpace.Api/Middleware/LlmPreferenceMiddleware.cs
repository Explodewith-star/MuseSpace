using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Api.Middleware;

/// <summary>
/// 在每个请求开始时根据 JWT 用户加载其 LLM 偏好到 Scoped 的 <see cref="LlmProviderSelector"/>。
/// 游客请求（无 JWT 或非法 userId）保留默认值。
/// </summary>
public sealed class LlmPreferenceMiddleware
{
    private readonly RequestDelegate _next;

    public LlmPreferenceMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        LlmProviderSelector selector,
        MuseSpaceDbContext db)
    {
        var userIdStr = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdStr, out var userId))
        {
            var pref = await db.UserLlmPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId, context.RequestAborted);

            if (pref is not null
                && Enum.TryParse<LlmProviderType>(pref.Provider, ignoreCase: true, out var provider))
            {
                selector.Active = provider;
                selector.ActiveModel = pref.ModelId;
            }
        }

        await _next(context);
    }
}
