using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <summary>
    /// 补记迁移（Backfill）：补录历史上绕过 EF 直接 SQL 操作的对象。
    /// 涉及对象：
    ///   - 表：chapter_batch_draft_runs / feature_flags / plot_threads
    ///   - 列：agent_runs.InputFull / agent_runs.OutputFull
    /// 全部使用 IF NOT EXISTS，对现有数据库为 no-op；对全量重建场景能正确创建。
    /// 详见 docs/EF-MigrationGuide.md 第六节。
    /// </summary>
    public partial class BackfillHistoricalDrift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===== agent_runs 漂移列 =====
            migrationBuilder.Sql(@"ALTER TABLE agent_runs ADD COLUMN IF NOT EXISTS ""InputFull"" text;");
            migrationBuilder.Sql(@"ALTER TABLE agent_runs ADD COLUMN IF NOT EXISTS ""OutputFull"" text;");

            // ===== chapter_batch_draft_runs 漂移表 =====
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS chapter_batch_draft_runs (
                    ""Id"" uuid NOT NULL,
                    ""StoryProjectId"" uuid NOT NULL,
                    ""UserId"" uuid NULL,
                    ""FromNumber"" integer NOT NULL,
                    ""ToNumber"" integer NOT NULL,
                    ""SkipChaptersWithDraft"" boolean NOT NULL,
                    ""TotalCount"" integer NOT NULL,
                    ""CompletedCount"" integer NOT NULL,
                    ""FailedCount"" integer NOT NULL,
                    ""SkippedCount"" integer NOT NULL,
                    ""FailedChapterIds"" uuid[] NULL,
                    ""CurrentChapterId"" uuid NULL,
                    ""Status"" integer NOT NULL,
                    ""CancelRequested"" boolean NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""StartedAt"" timestamp with time zone NULL,
                    ""FinishedAt"" timestamp with time zone NULL,
                    ""ErrorMessage"" text NULL,
                    CONSTRAINT ""PK_chapter_batch_draft_runs"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_chapter_batch_draft_runs_story_projects_StoryProjectId""
                        FOREIGN KEY (""StoryProjectId"") REFERENCES story_projects(""Id"") ON DELETE CASCADE
                );");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_chapter_batch_draft_runs_StoryProjectId"" ON chapter_batch_draft_runs (""StoryProjectId"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_chapter_batch_draft_runs_StoryProjectId_Status"" ON chapter_batch_draft_runs (""StoryProjectId"", ""Status"");");

            // ===== feature_flags 漂移表 =====
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS feature_flags (
                    ""Key"" character varying(100) NOT NULL,
                    ""Description"" character varying(500) NULL,
                    ""IsEnabled"" boolean NOT NULL,
                    ""UpdatedAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_feature_flags"" PRIMARY KEY (""Key"")
                );");

            // ===== plot_threads 漂移表 =====
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS plot_threads (
                    ""Id"" uuid NOT NULL,
                    ""StoryProjectId"" uuid NOT NULL,
                    ""Title"" character varying(500) NOT NULL,
                    ""Description"" text NULL,
                    ""Importance"" character varying(20) NULL,
                    ""Status"" integer NOT NULL,
                    ""PlantedInChapterId"" uuid NULL,
                    ""ResolvedInChapterId"" uuid NULL,
                    ""RelatedCharacterIds"" uuid[] NULL,
                    ""ExpectedResolveByChapterNumber"" integer NULL,
                    ""Tags"" character varying(500) NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_plot_threads"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_plot_threads_story_projects_StoryProjectId""
                        FOREIGN KEY (""StoryProjectId"") REFERENCES story_projects(""Id"") ON DELETE CASCADE
                );");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_plot_threads_StoryProjectId"" ON plot_threads (""StoryProjectId"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_plot_threads_StoryProjectId_Status"" ON plot_threads (""StoryProjectId"", ""Status"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS plot_threads;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS feature_flags;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS chapter_batch_draft_runs;");
            migrationBuilder.Sql(@"ALTER TABLE agent_runs DROP COLUMN IF EXISTS ""OutputFull"";");
            migrationBuilder.Sql(@"ALTER TABLE agent_runs DROP COLUMN IF EXISTS ""InputFull"";");
        }
    }
}