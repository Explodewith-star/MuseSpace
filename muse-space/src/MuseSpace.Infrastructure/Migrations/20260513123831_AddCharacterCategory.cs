using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "characters",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "characters");
        }
    }
}
