using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillWorldRuleSourceNovelId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 回填：将已采纳的 WorldRule 类建议中的 SourceNovelId 写入 world_rules 表
            migrationBuilder.Sql("""
                UPDATE world_rules w
                SET "SourceNovelId" = s."SourceNovelId"
                FROM agent_suggestions s
                WHERE s."TargetEntityId" = w."Id"
                  AND s."Category" = 'WorldRule'
                  AND s."SourceNovelId" IS NOT NULL
                  AND w."SourceNovelId" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
