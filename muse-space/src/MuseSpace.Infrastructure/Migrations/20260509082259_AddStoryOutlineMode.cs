using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoryOutlineMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "story_outlines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    SourceNovelId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceRangeStart = table.Column<int>(type: "integer", nullable: true),
                    SourceRangeEnd = table.Column<int>(type: "integer", nullable: true),
                    BranchTopic = table.Column<string>(type: "text", nullable: true),
                    ContinuationAnchor = table.Column<string>(type: "text", nullable: true),
                    DivergencePolicy = table.Column<int>(type: "integer", nullable: false),
                    TargetChapterCount = table.Column<int>(type: "integer", nullable: true),
                    OutlineSummary = table.Column<string>(type: "text", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_story_outlines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_story_outlines_novels_SourceNovelId",
                        column: x => x.SourceNovelId,
                        principalTable: "novels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_story_outlines_story_projects_StoryProjectId",
                        column: x => x.StoryProjectId,
                        principalTable: "story_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.DropIndex(
                name: "IX_chapters_StoryProjectId_Number",
                table: "chapters");

            migrationBuilder.AddColumn<int>(
                name: "AllowedRevealLevel",
                table: "chapters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StoryOutlineId",
                table: "chapters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StoryOutlineId",
                table: "chapter_batch_draft_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO story_outlines (
                    "Id", "StoryProjectId", "Name", "Mode", "DivergencePolicy",
                    "IsDefault", "CreatedAt", "UpdatedAt")
                SELECT uuid_generate_v4(), sp."Id", '原创主线', 0, 1, TRUE, NOW(), NOW()
                FROM story_projects sp
                WHERE NOT EXISTS (
                    SELECT 1 FROM story_outlines so
                    WHERE so."StoryProjectId" = sp."Id" AND so."IsDefault" = TRUE
                );
                """);

            migrationBuilder.Sql("""
                UPDATE chapters c
                SET "StoryOutlineId" = so."Id"
                FROM story_outlines so
                WHERE so."StoryProjectId" = c."StoryProjectId"
                  AND so."IsDefault" = TRUE
                  AND c."StoryOutlineId" IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE chapter_batch_draft_runs r
                SET "StoryOutlineId" = so."Id"
                FROM story_outlines so
                WHERE so."StoryProjectId" = r."StoryProjectId"
                  AND so."IsDefault" = TRUE
                  AND r."StoryOutlineId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "StoryOutlineId",
                table: "chapters",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StoryOutlineId",
                table: "chapter_batch_draft_runs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_chapters_StoryOutlineId",
                table: "chapters",
                column: "StoryOutlineId");

            migrationBuilder.CreateIndex(
                name: "IX_chapters_StoryOutlineId_Number",
                table: "chapters",
                columns: new[] { "StoryOutlineId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chapter_batch_draft_runs_StoryOutlineId",
                table: "chapter_batch_draft_runs",
                column: "StoryOutlineId");

            migrationBuilder.CreateIndex(
                name: "IX_chapter_batch_draft_runs_StoryOutlineId_Status",
                table: "chapter_batch_draft_runs",
                columns: new[] { "StoryOutlineId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_story_outlines_SourceNovelId",
                table: "story_outlines",
                column: "SourceNovelId");

            migrationBuilder.CreateIndex(
                name: "IX_story_outlines_StoryProjectId",
                table: "story_outlines",
                column: "StoryProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_story_outlines_StoryProjectId_IsDefault",
                table: "story_outlines",
                columns: new[] { "StoryProjectId", "IsDefault" },
                unique: true,
                filter: "\"IsDefault\" = TRUE");

            migrationBuilder.AddForeignKey(
                name: "FK_chapter_batch_draft_runs_story_outlines_StoryOutlineId",
                table: "chapter_batch_draft_runs",
                column: "StoryOutlineId",
                principalTable: "story_outlines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_chapters_story_outlines_StoryOutlineId",
                table: "chapters",
                column: "StoryOutlineId",
                principalTable: "story_outlines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chapter_batch_draft_runs_story_outlines_StoryOutlineId",
                table: "chapter_batch_draft_runs");

            migrationBuilder.DropForeignKey(
                name: "FK_chapters_story_outlines_StoryOutlineId",
                table: "chapters");

            migrationBuilder.DropTable(
                name: "story_outlines");

            migrationBuilder.DropIndex(
                name: "IX_chapters_StoryOutlineId",
                table: "chapters");

            migrationBuilder.DropIndex(
                name: "IX_chapters_StoryOutlineId_Number",
                table: "chapters");

            migrationBuilder.DropIndex(
                name: "IX_chapter_batch_draft_runs_StoryOutlineId",
                table: "chapter_batch_draft_runs");

            migrationBuilder.DropIndex(
                name: "IX_chapter_batch_draft_runs_StoryOutlineId_Status",
                table: "chapter_batch_draft_runs");

            migrationBuilder.DropColumn(
                name: "AllowedRevealLevel",
                table: "chapters");

            migrationBuilder.DropColumn(
                name: "StoryOutlineId",
                table: "chapters");

            migrationBuilder.DropColumn(
                name: "StoryOutlineId",
                table: "chapter_batch_draft_runs");

            migrationBuilder.CreateIndex(
                name: "IX_chapters_StoryProjectId_Number",
                table: "chapters",
                columns: new[] { "StoryProjectId", "Number" },
                unique: true);
        }
    }
}
