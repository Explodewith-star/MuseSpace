using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Api.Hangfire;

/// <summary>
/// 启动一次性数据迁移：把老的 SuggestionCategories.Consistency / Character 类目的
/// 一致性建议数据按 title 前缀拆分到新的 *Consistency 子类目。
///
/// 全部使用 idempotent SQL，重复执行无副作用。每次启动均跑一次（成本可忽略）。
/// </summary>
public sealed class LegacyConsistencyCategoryMigrationHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LegacyConsistencyCategoryMigrationHostedService> _logger;

    public LegacyConsistencyCategoryMigrationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<LegacyConsistencyCategoryMigrationHostedService> logger)
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

            await runner.RunOnceAsync("2026_04_30_split_legacy_consistency_categories", async (ctx, ct) =>
            {
                // 文风偏离 → StyleConsistency
                var styleAffected = await ctx.Database.ExecuteSqlRawAsync(
                    "UPDATE agent_suggestions SET category = 'StyleConsistency' " +
                    "WHERE category = 'Consistency' AND title LIKE '文风偏离%'",
                    ct);

                // 大纲冲突 → OutlineConsistency
                var outlineAffected = await ctx.Database.ExecuteSqlRawAsync(
                    "UPDATE agent_suggestions SET category = 'OutlineConsistency' " +
                    "WHERE category = 'Consistency' AND title LIKE '大纲%冲突：%'",
                    ct);

                // 其余 Consistency → WorldRuleConsistency
                var worldAffected = await ctx.Database.ExecuteSqlRawAsync(
                    "UPDATE agent_suggestions SET category = 'WorldRuleConsistency' " +
                    "WHERE category = 'Consistency'",
                    ct);

                // Character 类目里的角色冲突建议 → CharacterConsistency
                var charAffected = await ctx.Database.ExecuteSqlRawAsync(
                    "UPDATE agent_suggestions SET category = 'CharacterConsistency' " +
                    "WHERE category = 'Character' AND title LIKE '角色冲突：%'",
                    ct);

                _logger.LogInformation(
                    "[LegacyMigration] Suggestion categories split: style={S}, outline={O}, world={W}, char={C}",
                    styleAffected, outlineAffected, worldAffected, charAffected);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            // 表未建好或其它错误：不阻塞启动
            _logger.LogWarning(ex, "[LegacyMigration] Skipped consistency category migration: {Msg}", ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
