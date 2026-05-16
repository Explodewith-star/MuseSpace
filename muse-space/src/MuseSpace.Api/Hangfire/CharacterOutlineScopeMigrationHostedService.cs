using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Api.Hangfire;

/// <summary>
/// 一次性迁移：为 characters 表新增 StoryOutlineId 列（可空），
/// 将现有角色归属到项目的默认大纲，并移除旧的 Category 列。
/// StoryOutlineId = null 代表「原著角色池」，有值代表归属某个具体大纲。
/// 使用 SchemaMigrationRunner 保证每个版本只跑一次。
/// </summary>
public sealed class CharacterOutlineScopeMigrationHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CharacterOutlineScopeMigrationHostedService> _logger;

    public CharacterOutlineScopeMigrationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<CharacterOutlineScopeMigrationHostedService> logger)
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

            await runner.RunOnceAsync("2026_05_15_character_outline_scope_v2", async (ctx, ct) =>
            {
                // 1. 新增 StoryOutlineId 列（可空，null = 原著角色池）
                await ctx.Database.ExecuteSqlRawAsync(
                    """ALTER TABLE characters ADD COLUMN IF NOT EXISTS "StoryOutlineId" uuid NULL;""", ct);

                // 2. 将有 SourceNovelId 的角色保留在 null（原著角色池）
                // 将无 SourceNovelId 的角色归属到项目的默认大纲
                await ctx.Database.ExecuteSqlRawAsync("""
                    UPDATE characters c
                    SET "StoryOutlineId" = (
                        SELECT o."Id"
                        FROM story_outlines o
                        WHERE o."StoryProjectId" = c."StoryProjectId"
                          AND o."IsDefault" = true
                        LIMIT 1
                    )
                    WHERE c."StoryOutlineId" IS NULL
                      AND c."SourceNovelId" IS NULL;
                    """, ct);

                // 3. 兜底：仍未归属的原创角色（没有默认大纲）用最早的大纲
                await ctx.Database.ExecuteSqlRawAsync("""
                    UPDATE characters c
                    SET "StoryOutlineId" = (
                        SELECT o."Id"
                        FROM story_outlines o
                        WHERE o."StoryProjectId" = c."StoryProjectId"
                        ORDER BY o."CreatedAt"
                        LIMIT 1
                    )
                    WHERE c."StoryOutlineId" IS NULL
                      AND c."SourceNovelId" IS NULL;
                    """, ct);

                // 4. 建索引（幂等）
                await ctx.Database.ExecuteSqlRawAsync(
                    """CREATE INDEX IF NOT EXISTS "IX_characters_StoryOutlineId" ON characters("StoryOutlineId");""", ct);

                // 5. 外键（可空，原著角色池的 null 行不触发外键）
                await ctx.Database.ExecuteSqlRawAsync("""
                    DO $$
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1 FROM pg_constraint
                            WHERE conname = 'FK_characters_story_outlines_StoryOutlineId'
                        ) THEN
                            ALTER TABLE characters
                            ADD CONSTRAINT "FK_characters_story_outlines_StoryOutlineId"
                            FOREIGN KEY ("StoryOutlineId") REFERENCES story_outlines("Id")
                            ON DELETE CASCADE;
                        END IF;
                    END $$;
                    """, ct);

                // 6. 移除旧 Category 列
                await ctx.Database.ExecuteSqlRawAsync(
                    """ALTER TABLE characters DROP COLUMN IF EXISTS "Category";""", ct);

                _logger.LogInformation("[Migration] characters.StoryOutlineId (nullable) migration applied successfully");
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Migration] CharacterOutlineScope migration failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
