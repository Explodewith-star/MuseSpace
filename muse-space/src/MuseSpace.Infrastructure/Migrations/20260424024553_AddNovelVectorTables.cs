using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNovelVectorTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add HNSW cosine index on embedding column for approximate nearest neighbour search.
            // ef_construction=64 and m=16 are sensible defaults for 1024-dim vectors.
            // Wrapped in DO block so the migration is skipped gracefully if the table/column doesn't exist yet.
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'memory'
      AND table_name   = 'chunk_embeddings'
      AND column_name  = 'embedding'
  ) THEN
    CREATE INDEX IF NOT EXISTS idx_chunk_embeddings_hnsw
    ON memory.chunk_embeddings USING hnsw (embedding vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);
  END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS memory.idx_chunk_embeddings_hnsw;");
        }
    }
}
