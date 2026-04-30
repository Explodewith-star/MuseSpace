using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Api.Hangfire;

/// <summary>
/// 启动时 idempotent 建表：D4-C 伏笔追踪所需的 plot_threads 表。
/// 项目本身不使用 EF Migration，所以采用 CREATE TABLE IF NOT EXISTS 模式。
/// </summary>
public sealed class PlotThreadSchemaInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlotThreadSchemaInitializerHostedService> _logger;

    public PlotThreadSchemaInitializerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<PlotThreadSchemaInitializerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MuseSpaceDbContext>();
            var runner = new SchemaMigrationRunner(db, _logger);

            await runner.RunOnceAsync("2026_04_30_plot_threads_and_feature_flags", async (ctx, ct) =>
            {
                const string sql = """
                    CREATE TABLE IF NOT EXISTS plot_threads (
                        "Id" uuid PRIMARY KEY,
                        "StoryProjectId" uuid NOT NULL REFERENCES story_projects("Id") ON DELETE CASCADE,
                        "Title" varchar(500) NOT NULL,
                        "Description" text,
                        "Importance" varchar(20),
                        "Status" int NOT NULL DEFAULT 0,
                        "PlantedInChapterId" uuid,
                        "ResolvedInChapterId" uuid,
                        "RelatedCharacterIds" uuid[],
                        "Tags" varchar(500),
                        "CreatedAt" timestamptz NOT NULL DEFAULT now(),
                        "UpdatedAt" timestamptz NOT NULL DEFAULT now()
                    );

                    CREATE INDEX IF NOT EXISTS ix_plot_threads_project ON plot_threads("StoryProjectId");
                    CREATE INDEX IF NOT EXISTS ix_plot_threads_project_status ON plot_threads("StoryProjectId", "Status");

                    CREATE TABLE IF NOT EXISTS feature_flags (
                        "Key" varchar(100) PRIMARY KEY,
                        "Description" varchar(500),
                        "IsEnabled" boolean NOT NULL DEFAULT false,
                        "UpdatedAt" timestamptz NOT NULL DEFAULT now()
                    );
                    """;
                await ctx.Database.ExecuteSqlRawAsync(sql, ct);
            }, cancellationToken);

            // P2：AgentRun 列表按 StartedAt DESC 排序，补一个索引（独立 version，可重复加新 version）
            await runner.RunOnceAsync("2026_04_30_agent_runs_started_at_index", async (ctx, ct) =>
            {
                await ctx.Database.ExecuteSqlRawAsync(
                    "CREATE INDEX IF NOT EXISTS ix_agent_runs_started_at_desc ON agent_runs(\"StartedAt\" DESC)",
                    ct);
            }, cancellationToken);

            // D4-D2 深化：补 InputFull / OutputFull 两列承载完整 Prompt / Response，用于管理员明细查看。
            await runner.RunOnceAsync("2026_04_30_agent_runs_full_input_output", async (ctx, ct) =>
            {
                await ctx.Database.ExecuteSqlRawAsync(
                    """
                    ALTER TABLE agent_runs ADD COLUMN IF NOT EXISTS "InputFull" text;
                    ALTER TABLE agent_runs ADD COLUMN IF NOT EXISTS "OutputFull" text;
                    """,
                    ct);
            }, cancellationToken);

            // D4-1 深化：补 plot_threads.ExpectedResolveByChapterNumber，用于过期提醒。
            await runner.RunOnceAsync("2026_04_30_plot_threads_expected_resolve_by", async (ctx, ct) =>
            {
                await ctx.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE plot_threads ADD COLUMN IF NOT EXISTS \"ExpectedResolveByChapterNumber\" int",
                    ct);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PlotThreadSchema] Skipped schema init: {Msg}", ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
