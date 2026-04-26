using Microsoft.EntityFrameworkCore;
using MuseSpace.Domain.Entities;
using MuseSpace.Infrastructure.Persistence.Entities;

namespace MuseSpace.Infrastructure.Persistence;

public class MuseSpaceDbContext : DbContext
{
    public MuseSpaceDbContext(DbContextOptions<MuseSpaceDbContext> options) : base(options) { }

    // ── public schema：现有业务实体 ─────────────────────────────────────────
    public DbSet<StoryProject> StoryProjects => Set<StoryProject>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Scene> Scenes => Set<Scene>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<StyleProfile> StyleProfiles => Set<StyleProfile>();
    public DbSet<WorldRule> WorldRules => Set<WorldRule>();
    public DbSet<GenerationRecord> GenerationRecords => Set<GenerationRecord>();

    // ── 用户认证 ───────────────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<UserLlmPreference> UserLlmPreferences => Set<UserLlmPreference>();

    // ── Agent 运行记录 ──────────────────────────────────────────────────────────
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();

    // ── public schema：原著导入 ───────────────────────────────────────────────
    public DbSet<Novel> Novels => Set<Novel>();
    public DbSet<NovelChunk> NovelChunks => Set<NovelChunk>();

    // ── memory schema：向量检索 ───────────────────────────────────────────────
    public DbSet<NovelChunkEmbedding> ChunkEmbeddings => Set<NovelChunkEmbedding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── StoryProject ─────────────────────────────────────────────────────
        modelBuilder.Entity<StoryProject>(entity =>
        {
            entity.ToTable("story_projects");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.Genre).HasMaxLength(200);
            entity.Property(e => e.NarrativePerspective).HasMaxLength(200);
            entity.Property(e => e.UserId).IsRequired(false);
            entity.HasIndex(e => e.UserId);
        });

        // ── Chapter ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Chapter>(entity =>
        {
            entity.ToTable("chapters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.Goal).HasColumnType("text");
            entity.Property(e => e.Summary).HasColumnType("text");
            entity.Property(e => e.DraftText).HasColumnType("text");
            entity.Property(e => e.FinalText).HasColumnType("text");
            entity.HasIndex(e => e.StoryProjectId);
            entity.HasIndex(e => new { e.StoryProjectId, e.Number }).IsUnique();
        });

        // ── Scene ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Scene>(entity =>
        {
            entity.ToTable("scenes");
            entity.HasKey(e => e.Id);
            // List<Guid> 映射为 PostgreSQL uuid[] 数组列
            entity.Property(e => e.InvolvedCharacterIds).HasColumnType("uuid[]");
            entity.Property(e => e.Goal).HasColumnType("text");
            entity.Property(e => e.Conflict).HasColumnType("text");
            entity.Property(e => e.EmotionCurve).HasColumnType("text");
            entity.Property(e => e.DraftText).HasColumnType("text");
            entity.Property(e => e.FinalText).HasColumnType("text");
            entity.HasIndex(e => e.ChapterId);
            entity.HasIndex(e => new { e.ChapterId, e.Sequence });
        });

        // ── Character ────────────────────────────────────────────────────────
        modelBuilder.Entity<Character>(entity =>
        {
            entity.ToTable("characters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(200);
            entity.Property(e => e.Tags).HasMaxLength(500);
            entity.Property(e => e.PersonalitySummary).HasColumnType("text");
            entity.Property(e => e.Motivation).HasColumnType("text");
            entity.Property(e => e.SpeakingStyle).HasColumnType("text");
            entity.Property(e => e.ForbiddenBehaviors).HasColumnType("text");
            entity.Property(e => e.PublicSecrets).HasColumnType("text");
            entity.Property(e => e.PrivateSecrets).HasColumnType("text");
            entity.Property(e => e.CurrentState).HasColumnType("text");
            entity.HasIndex(e => e.StoryProjectId);
        });

        // ── StyleProfile ─────────────────────────────────────────────────────
        modelBuilder.Entity<StyleProfile>(entity =>
        {
            entity.ToTable("style_profiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.SampleReferenceText).HasColumnType("text");
            entity.Property(e => e.ForbiddenExpressions).HasColumnType("text");
            entity.HasIndex(e => e.StoryProjectId);
        });

        // ── WorldRule ────────────────────────────────────────────────────────
        modelBuilder.Entity<WorldRule>(entity =>
        {
            entity.ToTable("world_rules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(200);
            entity.Property(e => e.Description).HasColumnType("text");
            entity.HasIndex(e => e.StoryProjectId);
        });

        // ── GenerationRecord ─────────────────────────────────────────────────
        modelBuilder.Entity<GenerationRecord>(entity =>
        {
            entity.ToTable("generation_records");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RequestId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.TaskType).HasMaxLength(200);
            entity.Property(e => e.SkillName).HasMaxLength(200);
            entity.Property(e => e.PromptName).HasMaxLength(200);
            entity.Property(e => e.PromptVersion).HasMaxLength(100);
            entity.Property(e => e.ModelName).HasMaxLength(300);
            entity.Property(e => e.InputPreview).HasColumnType("text");
            entity.Property(e => e.OutputPreview).HasColumnType("text");
            entity.HasIndex(e => e.StoryProjectId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // ── Novel ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Novel>(entity =>
        {
            entity.ToTable("novels");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.FileKey).HasMaxLength(1000);
            entity.Property(e => e.FileHash).HasMaxLength(64);
            entity.Property(e => e.LastError).HasColumnType("text");
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasIndex(e => e.StoryProjectId);
            // 同一项目内同一文件 hash 不允许重复导入
            entity.HasIndex(e => new { e.StoryProjectId, e.FileHash })
                  .HasFilter("\"FileHash\" IS NOT NULL");
        });

        // ── NovelChunk ───────────────────────────────────────────────────────
        modelBuilder.Entity<NovelChunk>(entity =>
        {
            entity.ToTable("novel_chunks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).HasColumnType("text").IsRequired();
            entity.HasIndex(e => new { e.NovelId, e.ChunkIndex }).IsUnique();
            entity.HasIndex(e => new { e.StoryProjectId, e.IsEmbedded });
        });

        // ── NovelChunkEmbedding（memory schema）──────────────────────────────
        modelBuilder.Entity<NovelChunkEmbedding>(entity =>
        {
            entity.ToTable("chunk_embeddings", "memory");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ModelName).HasMaxLength(200).IsRequired();
            // 向量列：1024 维，对应 BAAI/bge-m3
            entity.Property(e => e.Embedding).HasColumnType("vector(1024)");
            entity.HasIndex(e => e.ChunkId);
            entity.HasIndex(e => new { e.StoryProjectId, e.ModelName });
            // HNSW 余弦索引请在 Migration 生成后，手动在 Up() 中追加：
            // migrationBuilder.Sql(
            //   "CREATE INDEX idx_chunk_embeddings_hnsw " +
            //   "ON memory.chunk_embeddings USING hnsw (embedding vector_cosine_ops) " +
            //   "WITH (m = 16, ef_construction = 64);");
        });

        // ── User ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.PhoneNumber).IsUnique();
            entity.Property(e => e.Role).HasMaxLength(10).IsRequired().HasDefaultValue("User");
        });

        // ── UserLlmPreference ─────────────────────────────────────────────────
        modelBuilder.Entity<UserLlmPreference>(entity =>
        {
            entity.ToTable("user_llm_preferences");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.Provider).HasMaxLength(20).IsRequired().HasDefaultValue("OpenRouter");
            entity.Property(e => e.ModelId).HasMaxLength(100);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── AgentRun ──────────────────────────────────────────────────────────
        modelBuilder.Entity<AgentRun>(entity =>
        {
            entity.ToTable("agent_runs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AgentName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.InputPreview).HasColumnType("text");
            entity.Property(e => e.OutputPreview).HasColumnType("text");
            entity.Property(e => e.ErrorMessage).HasColumnType("text");
            entity.HasIndex(e => e.AgentName);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.StartedAt);
        });
    }
}
