using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonFactLayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "canon_facts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    FactKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FactValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    InvalidatedByChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canon_facts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_canon_facts_story_projects_StoryProjectId",
                        column: x => x.StoryProjectId,
                        principalTable: "story_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chapter_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EventText = table.Column<string>(type: "text", nullable: false),
                    ActorCharacterIds = table.Column<List<Guid>>(type: "uuid[]", nullable: true),
                    TargetCharacterIds = table.Column<List<Guid>>(type: "uuid[]", nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TimePoint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Importance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsIrreversible = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chapter_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chapter_events_chapters_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "chapters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chapter_events_story_projects_StoryProjectId",
                        column: x => x.StoryProjectId,
                        principalTable: "story_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_canon_facts_StoryProjectId",
                table: "canon_facts",
                column: "StoryProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_canon_facts_StoryProjectId_FactType",
                table: "canon_facts",
                columns: new[] { "StoryProjectId", "FactType" });

            migrationBuilder.CreateIndex(
                name: "IX_canon_facts_StoryProjectId_FactType_FactKey",
                table: "canon_facts",
                columns: new[] { "StoryProjectId", "FactType", "FactKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_canon_facts_StoryProjectId_IsLocked",
                table: "canon_facts",
                columns: new[] { "StoryProjectId", "IsLocked" });

            migrationBuilder.CreateIndex(
                name: "IX_chapter_events_ChapterId",
                table: "chapter_events",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_chapter_events_StoryProjectId",
                table: "chapter_events",
                column: "StoryProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_chapter_events_StoryProjectId_ChapterId",
                table: "chapter_events",
                columns: new[] { "StoryProjectId", "ChapterId" });

            migrationBuilder.CreateIndex(
                name: "IX_chapter_events_StoryProjectId_IsIrreversible",
                table: "chapter_events",
                columns: new[] { "StoryProjectId", "IsIrreversible" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "canon_facts");

            migrationBuilder.DropTable(
                name: "chapter_events");
        }
    }
}
