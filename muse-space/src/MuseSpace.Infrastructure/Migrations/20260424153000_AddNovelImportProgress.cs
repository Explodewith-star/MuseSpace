using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MuseSpace.Infrastructure.Persistence;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MuseSpaceDbContext))]
    [Migration("20260424153000_AddNovelImportProgress")]
    public partial class AddNovelImportProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FinishedAt",
                table: "novels",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "novels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressDone",
                table: "novels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProgressTotal",
                table: "novels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "novels",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinishedAt",
                table: "novels");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "novels");

            migrationBuilder.DropColumn(
                name: "ProgressDone",
                table: "novels");

            migrationBuilder.DropColumn(
                name: "ProgressTotal",
                table: "novels");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "novels");
        }
    }
}
