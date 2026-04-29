using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChapterSourceSuggestionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<List<string>>(
                name: "MustIncludePoints",
                table: "chapters",
                type: "text[]",
                nullable: true,
                oldClrType: typeof(List<string>),
                oldType: "text[]");

            migrationBuilder.AlterColumn<List<Guid>>(
                name: "KeyCharacterIds",
                table: "chapters",
                type: "uuid[]",
                nullable: true,
                oldClrType: typeof(List<Guid>),
                oldType: "uuid[]");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceSuggestionId",
                table: "chapters",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_chapters_SourceSuggestionId",
                table: "chapters",
                column: "SourceSuggestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_chapters_SourceSuggestionId",
                table: "chapters");

            migrationBuilder.DropColumn(
                name: "SourceSuggestionId",
                table: "chapters");

            migrationBuilder.AlterColumn<List<string>>(
                name: "MustIncludePoints",
                table: "chapters",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "text[]",
                oldNullable: true);

            migrationBuilder.AlterColumn<List<Guid>>(
                name: "KeyCharacterIds",
                table: "chapters",
                type: "uuid[]",
                nullable: false,
                oldClrType: typeof(List<Guid>),
                oldType: "uuid[]",
                oldNullable: true);
        }
    }
}
