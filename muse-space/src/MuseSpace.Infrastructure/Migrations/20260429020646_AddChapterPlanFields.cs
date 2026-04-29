using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChapterPlanFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Conflict",
                table: "chapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmotionCurve",
                table: "chapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<Guid>>(
                name: "KeyCharacterIds",
                table: "chapters",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'::uuid[]");

            migrationBuilder.AddColumn<List<string>>(
                name: "MustIncludePoints",
                table: "chapters",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Conflict",
                table: "chapters");

            migrationBuilder.DropColumn(
                name: "EmotionCurve",
                table: "chapters");

            migrationBuilder.DropColumn(
                name: "KeyCharacterIds",
                table: "chapters");

            migrationBuilder.DropColumn(
                name: "MustIncludePoints",
                table: "chapters");
        }
    }
}
