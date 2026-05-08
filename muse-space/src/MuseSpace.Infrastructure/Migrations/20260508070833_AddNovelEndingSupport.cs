using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNovelEndingSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EndingSummary",
                table: "novels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StyleSummary",
                table: "novels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SummaryGeneratedAt",
                table: "novels",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "novel_character_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NovelId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EndingState = table.Column<string>(type: "text", nullable: false),
                    IsIrreversible = table.Column<bool>(type: "boolean", nullable: false),
                    LinkedCharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_novel_character_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_novel_character_snapshots_novels_NovelId",
                        column: x => x.NovelId,
                        principalTable: "novels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_novel_character_snapshots_NovelId",
                table: "novel_character_snapshots",
                column: "NovelId");

            migrationBuilder.CreateIndex(
                name: "IX_novel_character_snapshots_StoryProjectId",
                table: "novel_character_snapshots",
                column: "StoryProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "novel_character_snapshots");

            migrationBuilder.DropColumn(
                name: "EndingSummary",
                table: "novels");

            migrationBuilder.DropColumn(
                name: "StyleSummary",
                table: "novels");

            migrationBuilder.DropColumn(
                name: "SummaryGeneratedAt",
                table: "novels");
        }
    }
}
