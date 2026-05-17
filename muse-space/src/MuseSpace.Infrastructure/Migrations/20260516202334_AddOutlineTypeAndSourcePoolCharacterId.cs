using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutlineTypeAndSourcePoolCharacterId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        // Category 列可能已不存在（历史迁移未实际应用），用 IF EXISTS 保证幂等
        migrationBuilder.Sql(@"ALTER TABLE characters DROP COLUMN IF EXISTS ""Category"";");

        // OutlineType：新列
        migrationBuilder.AddColumn<string>(
            name: "OutlineType",
            table: "story_projects",
            type: "text",
            nullable: true);

        // SourcePoolCharacterId：新列
        migrationBuilder.AddColumn<Guid>(
            name: "SourcePoolCharacterId",
            table: "characters",
            type: "uuid",
            nullable: true);

        // StoryOutlineId 可能已通过之前的 raw-SQL 迁移添加，用 IF NOT EXISTS 保证幂等
        migrationBuilder.Sql(@"ALTER TABLE characters ADD COLUMN IF NOT EXISTS ""StoryOutlineId"" uuid NULL;");

        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_characters_StoryOutlineId"" ON characters(""StoryOutlineId"");");

        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.table_constraints
                    WHERE constraint_name = 'FK_characters_story_outlines_StoryOutlineId'
                ) THEN
                    ALTER TABLE characters
                        ADD CONSTRAINT ""FK_characters_story_outlines_StoryOutlineId""
                        FOREIGN KEY (""StoryOutlineId"")
                        REFERENCES story_outlines(""Id"") ON DELETE CASCADE;
                END IF;
            END $$;
        ");
    }

    /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_characters_story_outlines_StoryOutlineId",
                table: "characters");

            migrationBuilder.DropIndex(
                name: "IX_characters_StoryOutlineId",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "OutlineType",
                table: "story_projects");

            migrationBuilder.DropColumn(
                name: "SourcePoolCharacterId",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "StoryOutlineId",
                table: "characters");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "characters",
                type: "text",
                nullable: true);
        }
    }
}
