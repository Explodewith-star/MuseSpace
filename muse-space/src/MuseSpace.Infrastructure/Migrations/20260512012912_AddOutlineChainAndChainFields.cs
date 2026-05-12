using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutlineChainAndChainFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChainId",
                table: "story_outlines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChainIndex",
                table: "story_outlines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousOutlineId",
                table: "story_outlines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "outline_chains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outline_chains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_outline_chains_story_projects_StoryProjectId",
                        column: x => x.StoryProjectId,
                        principalTable: "story_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_story_outlines_ChainId",
                table: "story_outlines",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_outline_chains_StoryProjectId",
                table: "outline_chains",
                column: "StoryProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_story_outlines_outline_chains_ChainId",
                table: "story_outlines",
                column: "ChainId",
                principalTable: "outline_chains",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_story_outlines_outline_chains_ChainId",
                table: "story_outlines");

            migrationBuilder.DropTable(
                name: "outline_chains");

            migrationBuilder.DropIndex(
                name: "IX_story_outlines_ChainId",
                table: "story_outlines");

            migrationBuilder.DropColumn(
                name: "ChainId",
                table: "story_outlines");

            migrationBuilder.DropColumn(
                name: "ChainIndex",
                table: "story_outlines");

            migrationBuilder.DropColumn(
                name: "PreviousOutlineId",
                table: "story_outlines");
        }
    }
}
