using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlotThreadVisibilityScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChainId",
                table: "plot_threads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OutlineId",
                table: "plot_threads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResolvedInOutlineId",
                table: "plot_threads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "plot_threads",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_plot_threads_StoryProjectId_ChainId",
                table: "plot_threads",
                columns: new[] { "StoryProjectId", "ChainId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_plot_threads_StoryProjectId_ChainId",
                table: "plot_threads");

            migrationBuilder.DropColumn(
                name: "ChainId",
                table: "plot_threads");

            migrationBuilder.DropColumn(
                name: "OutlineId",
                table: "plot_threads");

            migrationBuilder.DropColumn(
                name: "ResolvedInOutlineId",
                table: "plot_threads");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "plot_threads");
        }
    }
}
