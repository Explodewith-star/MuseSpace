using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCascadeForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_agent_suggestions_story_projects_StoryProjectId",
                table: "agent_suggestions",
                column: "StoryProjectId",
                principalTable: "story_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_chapters_story_projects_StoryProjectId",
                table: "chapters",
                column: "StoryProjectId",
                principalTable: "story_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_characters_story_projects_StoryProjectId",
                table: "characters",
                column: "StoryProjectId",
                principalTable: "story_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_generation_records_story_projects_StoryProjectId",
                table: "generation_records",
                column: "StoryProjectId",
                principalTable: "story_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_novels_story_projects_StoryProjectId",
                table: "novels",
                column: "StoryProjectId",
                principalTable: "story_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_style_profiles_story_projects_StoryProjectId",
                table: "style_profiles",
                column: "StoryProjectId",
                principalTable: "story_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_world_rules_story_projects_StoryProjectId",
                table: "world_rules",
                column: "StoryProjectId",
                principalTable: "story_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agent_suggestions_story_projects_StoryProjectId",
                table: "agent_suggestions");

            migrationBuilder.DropForeignKey(
                name: "FK_chapters_story_projects_StoryProjectId",
                table: "chapters");

            migrationBuilder.DropForeignKey(
                name: "FK_characters_story_projects_StoryProjectId",
                table: "characters");

            migrationBuilder.DropForeignKey(
                name: "FK_generation_records_story_projects_StoryProjectId",
                table: "generation_records");

            migrationBuilder.DropForeignKey(
                name: "FK_novels_story_projects_StoryProjectId",
                table: "novels");

            migrationBuilder.DropForeignKey(
                name: "FK_style_profiles_story_projects_StoryProjectId",
                table: "style_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_world_rules_story_projects_StoryProjectId",
                table: "world_rules");
        }
    }
}
