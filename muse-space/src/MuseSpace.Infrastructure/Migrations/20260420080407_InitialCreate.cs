using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 先开启所需 PostgreSQL 扩展
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");

            migrationBuilder.EnsureSchema(
                name: "memory");

            migrationBuilder.CreateTable(
                name: "chapters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Goal = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    DraftText = table.Column<string>(type: "text", nullable: true),
                    FinalText = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chapters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "characters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Age = table.Column<int>(type: "integer", nullable: true),
                    Role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PersonalitySummary = table.Column<string>(type: "text", nullable: true),
                    Motivation = table.Column<string>(type: "text", nullable: true),
                    SpeakingStyle = table.Column<string>(type: "text", nullable: true),
                    ForbiddenBehaviors = table.Column<string>(type: "text", nullable: true),
                    PublicSecrets = table.Column<string>(type: "text", nullable: true),
                    PrivateSecrets = table.Column<string>(type: "text", nullable: true),
                    CurrentState = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "chunk_embeddings",
                schema: "memory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1024)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunk_embeddings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "generation_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StoryProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SkillName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PromptName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PromptVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModelName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    InputPreview = table.Column<string>(type: "text", nullable: true),
                    OutputPreview = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generation_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "novel_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NovelId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CharCount = table.Column<int>(type: "integer", nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: true),
                    StartOffset = table.Column<int>(type: "integer", nullable: false),
                    EndOffset = table.Column<int>(type: "integer", nullable: false),
                    IsEmbedded = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_novel_chunks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "novels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FileKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalChunks = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_novels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "scenes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Goal = table.Column<string>(type: "text", nullable: true),
                    Conflict = table.Column<string>(type: "text", nullable: true),
                    EmotionCurve = table.Column<string>(type: "text", nullable: true),
                    DraftText = table.Column<string>(type: "text", nullable: true),
                    FinalText = table.Column<string>(type: "text", nullable: true),
                    InvolvedCharacterIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scenes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "story_projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Genre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NarrativePerspective = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DefaultStyleProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_story_projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "style_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SentenceLengthPreference = table.Column<string>(type: "text", nullable: true),
                    DialogueRatio = table.Column<string>(type: "text", nullable: true),
                    DescriptionDensity = table.Column<string>(type: "text", nullable: true),
                    Tone = table.Column<string>(type: "text", nullable: true),
                    ForbiddenExpressions = table.Column<string>(type: "text", nullable: true),
                    SampleReferenceText = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_style_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "world_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsHardConstraint = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_rules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chapters_StoryProjectId",
                table: "chapters",
                column: "StoryProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_chapters_StoryProjectId_Number",
                table: "chapters",
                columns: new[] { "StoryProjectId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_characters_StoryProjectId",
                table: "characters",
                column: "StoryProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_chunk_embeddings_ChunkId",
                schema: "memory",
                table: "chunk_embeddings",
                column: "ChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_chunk_embeddings_StoryProjectId_ModelName",
                schema: "memory",
                table: "chunk_embeddings",
                columns: new[] { "StoryProjectId", "ModelName" });

            migrationBuilder.CreateIndex(
                name: "IX_generation_records_CreatedAt",
                table: "generation_records",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_generation_records_StoryProjectId",
                table: "generation_records",
                column: "StoryProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_novel_chunks_NovelId_ChunkIndex",
                table: "novel_chunks",
                columns: new[] { "NovelId", "ChunkIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_novel_chunks_StoryProjectId_IsEmbedded",
                table: "novel_chunks",
                columns: new[] { "StoryProjectId", "IsEmbedded" });

            migrationBuilder.CreateIndex(
                name: "IX_novels_StoryProjectId",
                table: "novels",
                column: "StoryProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_novels_StoryProjectId_FileHash",
                table: "novels",
                columns: new[] { "StoryProjectId", "FileHash" },
                filter: "\"FileHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_scenes_ChapterId",
                table: "scenes",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_scenes_ChapterId_Sequence",
                table: "scenes",
                columns: new[] { "ChapterId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_style_profiles_StoryProjectId",
                table: "style_profiles",
                column: "StoryProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_world_rules_StoryProjectId",
                table: "world_rules",
                column: "StoryProjectId");

            // HNSW 余弦相似度索引，用于 BAAI/bge-m3 向量检索
            migrationBuilder.Sql(
                "CREATE INDEX idx_chunk_embeddings_hnsw " +
                "ON memory.chunk_embeddings USING hnsw (\"Embedding\" vector_cosine_ops) " +
                "WITH (m = 16, ef_construction = 64);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chapters");

            migrationBuilder.DropTable(
                name: "characters");

            migrationBuilder.DropTable(
                name: "chunk_embeddings",
                schema: "memory");

            migrationBuilder.DropTable(
                name: "generation_records");

            migrationBuilder.DropTable(
                name: "novel_chunks");

            migrationBuilder.DropTable(
                name: "novels");

            migrationBuilder.DropTable(
                name: "scenes");

            migrationBuilder.DropTable(
                name: "story_projects");

            migrationBuilder.DropTable(
                name: "style_profiles");

            migrationBuilder.DropTable(
                name: "world_rules");
        }
    }
}
