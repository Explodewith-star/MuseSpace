using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MuseSpace.Api.Extensions;
using MuseSpace.Api.Hangfire;
using MuseSpace.Api.Hubs;
using MuseSpace.Api.Middleware;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    builder.Services.AddOpenApi();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddMuseSpaceServices(builder.Configuration);

    // ── JWT 认证（Fail-Fast：未配置 Auth:JwtSecret 直接拒绝启动） ─────────────
    // 开发环境请在 appsettings.Development.json 或 user-secrets 设置 Auth:JwtSecret（≥32 字符）
    // 生产环境通过容器环境变量 Auth__JwtSecret 注入
    var jwtSecret = builder.Configuration["Auth:JwtSecret"];
    if (string.IsNullOrWhiteSpace(jwtSecret))
        throw new InvalidOperationException(
            "Auth:JwtSecret 未配置。请在 appsettings.Development.json / user-secrets / 环境变量（Auth__JwtSecret）中设置一个长度至少 32 字符的随机密钥。");
    if (jwtSecret.Length < 32)
        throw new InvalidOperationException(
            $"Auth:JwtSecret 长度过短（当前 {jwtSecret.Length}），需至少 32 字符以保证 HMAC-SHA256 安全性。");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };
            // SignalR 需要从 query string 读取 Token
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        context.Token = accessToken;
                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddAuthorization();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DevFrontend", policy =>
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()); // required for SignalR WebSocket
    });

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
        app.UseCors("DevFrontend");
        app.MapHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new AllowAllDashboardAuthorizationFilter()]
        });
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    // 按当前用户加载 LLM 偏好（必须在 UseAuthentication 之后）
    app.UseMiddleware<LlmPreferenceMiddleware>();
    app.MapControllers();
    app.MapHub<NovelImportHub>("/hubs/novel-import");
    app.MapHub<AgentProgressHub>("/hubs/agent-progress");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}
