using Hangfire.Dashboard;

namespace MuseSpace.Api.Hangfire;

/// <summary>
/// 开发环境 Hangfire Dashboard 授权过滤器：放行所有请求。
/// 生产环境请替换为需要身份验证的实现。
/// </summary>
public sealed class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
