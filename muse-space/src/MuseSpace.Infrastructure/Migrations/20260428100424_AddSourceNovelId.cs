using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceNovelId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: adds column only if it doesn't already exist
            migrationBuilder.Sql(@"
                ALTER TABLE agent_suggestions
                ADD COLUMN IF NOT EXISTS ""SourceNovelId"" uuid NULL;
            ");
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_agent_suggestions_SourceNovelId""
                ON agent_suggestions(""SourceNovelId"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_agent_suggestions_SourceNovelId"";");
            migrationBuilder.Sql(@"ALTER TABLE agent_suggestions DROP COLUMN IF EXISTS ""SourceNovelId"";");
        }
    }
}
