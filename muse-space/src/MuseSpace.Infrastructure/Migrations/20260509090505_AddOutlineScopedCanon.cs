using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutlineScopedCanon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_canon_facts_StoryProjectId_FactType_FactKey",
                table: "canon_facts");

            migrationBuilder.AddColumn<Guid>(
                name: "StoryOutlineId",
                table: "chapter_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StoryOutlineId",
                table: "canon_facts",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE chapter_events e
                SET "StoryOutlineId" = c."StoryOutlineId"
                FROM chapters c
                WHERE c."Id" = e."ChapterId"
                  AND c."StoryProjectId" = e."StoryProjectId"
                  AND e."StoryOutlineId" IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE canon_facts f
                SET "StoryOutlineId" = c."StoryOutlineId"
                FROM chapters c
                WHERE c."Id" = f."SourceChapterId"
                  AND c."StoryProjectId" = f."StoryProjectId"
                  AND f."StoryOutlineId" IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE canon_facts f
                SET "StoryOutlineId" = so."Id"
                FROM story_outlines so
                WHERE so."StoryProjectId" = f."StoryProjectId"
                  AND so."IsDefault" = TRUE
                  AND f."StoryOutlineId" IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE chapter_events e
                SET "StoryOutlineId" = so."Id"
                FROM story_outlines so
                WHERE so."StoryProjectId" = e."StoryProjectId"
                  AND so."IsDefault" = TRUE
                  AND e."StoryOutlineId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "StoryOutlineId",
                table: "chapter_events",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StoryOutlineId",
                table: "canon_facts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_chapter_events_StoryOutlineId",
                table: "chapter_events",
                column: "StoryOutlineId");

            migrationBuilder.CreateIndex(
                name: "IX_chapter_events_StoryOutlineId_ChapterId",
                table: "chapter_events",
                columns: new[] { "StoryOutlineId", "ChapterId" });

            migrationBuilder.CreateIndex(
                name: "IX_chapter_events_StoryOutlineId_IsIrreversible",
                table: "chapter_events",
                columns: new[] { "StoryOutlineId", "IsIrreversible" });

            migrationBuilder.CreateIndex(
                name: "IX_canon_facts_StoryOutlineId",
                table: "canon_facts",
                column: "StoryOutlineId");

            migrationBuilder.CreateIndex(
                name: "IX_canon_facts_StoryOutlineId_FactType",
                table: "canon_facts",
                columns: new[] { "StoryOutlineId", "FactType" });

            migrationBuilder.CreateIndex(
                name: "IX_canon_facts_StoryOutlineId_FactType_FactKey",
                table: "canon_facts",
                columns: new[] { "StoryOutlineId", "FactType", "FactKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_canon_facts_StoryOutlineId_IsLocked",
                table: "canon_facts",
                columns: new[] { "StoryOutlineId", "IsLocked" });

            migrationBuilder.AddForeignKey(
                name: "FK_canon_facts_story_outlines_StoryOutlineId",
                table: "canon_facts",
                column: "StoryOutlineId",
                principalTable: "story_outlines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_chapter_events_story_outlines_StoryOutlineId",
                table: "chapter_events",
                column: "StoryOutlineId",
                principalTable: "story_outlines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_canon_facts_story_outlines_StoryOutlineId",
                table: "canon_facts");

            migrationBuilder.DropForeignKey(
                name: "FK_chapter_events_story_outlines_StoryOutlineId",
                table: "chapter_events");

            migrationBuilder.DropIndex(
                name: "IX_chapter_events_StoryOutlineId",
                table: "chapter_events");

            migrationBuilder.DropIndex(
                name: "IX_chapter_events_StoryOutlineId_ChapterId",
                table: "chapter_events");

            migrationBuilder.DropIndex(
                name: "IX_chapter_events_StoryOutlineId_IsIrreversible",
                table: "chapter_events");

            migrationBuilder.DropIndex(
                name: "IX_canon_facts_StoryOutlineId",
                table: "canon_facts");

            migrationBuilder.DropIndex(
                name: "IX_canon_facts_StoryOutlineId_FactType",
                table: "canon_facts");

            migrationBuilder.DropIndex(
                name: "IX_canon_facts_StoryOutlineId_FactType_FactKey",
                table: "canon_facts");

            migrationBuilder.DropIndex(
                name: "IX_canon_facts_StoryOutlineId_IsLocked",
                table: "canon_facts");

            migrationBuilder.DropColumn(
                name: "StoryOutlineId",
                table: "chapter_events");

            migrationBuilder.DropColumn(
                name: "StoryOutlineId",
                table: "canon_facts");

            migrationBuilder.CreateIndex(
                name: "IX_canon_facts_StoryProjectId_FactType_FactKey",
                table: "canon_facts",
                columns: new[] { "StoryProjectId", "FactType", "FactKey" },
                unique: true);
        }
    }
}
