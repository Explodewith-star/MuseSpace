using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillCharacterSourceNovelId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 回填：将已采纳的 Character 类建议中的 SourceNovelId 写入 characters 表
            migrationBuilder.Sql("""
                UPDATE characters c
                SET "SourceNovelId" = s."SourceNovelId"
                FROM agent_suggestions s
                WHERE s."TargetEntityId" = c."Id"
                  AND s."Category" = 'Character'
                  AND s."SourceNovelId" IS NOT NULL
                  AND c."SourceNovelId" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
