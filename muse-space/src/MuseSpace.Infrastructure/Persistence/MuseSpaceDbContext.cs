using Microsoft.EntityFrameworkCore;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;
using MuseSpace.Infrastructure.Persistence.Entities;

namespace MuseSpace.Infrastructure.Persistence;

public class MuseSpaceDbContext : DbContext
{
    public MuseSpaceDbContext(DbContextOptions<MuseSpaceDbContext> options) : base(options) { }

    // ── public schema：现有业务实体 ─────────────────────────────────────────
    public DbSet<StoryProject> StoryProjects => Set<StoryProject>();
    public DbSet<StoryOutline> StoryOutlines => Set<StoryOutline>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Scene> Scenes => Set<Scene>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<StyleProfile> StyleProfiles => Set<StyleProfile>();
    public DbSet<WorldRule> WorldRules => Set<WorldRule>();
    public DbSet<PlotThread> PlotThreads => Set<PlotThread>();
    public DbSet<OutlineChain> OutlineChains => Set<OutlineChain>();
    public DbSet<ChapterEvent> ChapterEvents => Set<ChapterEvent>();
    public DbSet<CanonFact> CanonFacts => Set<CanonFact>();
    public DbSet<GenerationRecord> GenerationRecords => Set<GenerationRecord>();

    // ── 章节批量草稿生成 ──────────────────────────────────────────────────────
    public DbSet<ChapterBatchDraftRun> ChapterBatchDraftRuns => Set<ChapterBatchDraftRun>();

    // ── 用户认证 ───────────────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<UserLlmPreference> UserLlmPreferences => Set<UserLlmPreference>();

    // ── 功能开关 ────────────────────────────────────────────────────────────────
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    // ── Agent 运行记录 ──────────────────────────────────────────────────────────
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();

    // ── Agent 建议审核 ──────────────────────────────────────────────────────────
    public DbSet<AgentSuggestion> AgentSuggestions => Set<AgentSuggestion>();

    // ── public schema：原著导入 ───────────────────────────────────────────────
    public DbSet<Novel> Novels => Set<Novel>();
    public DbSet<NovelChunk> NovelChunks => Set<NovelChunk>();
    public DbSet<NovelCharacterSnapshot> NovelCharacterSnapshots => Set<NovelCharacterSnapshot>();

    // ── 后台任务跟踪 ─────────────────────────────────────────────────────────
    public DbSet<BackgroundTaskRecord> BackgroundTasks => Set<BackgroundTaskRecord>();

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

        // ── StoryOutline ────────────────────────────────────────────────────
        modelBuilder.Entity<StoryOutline>(entity =>
        {
            entity.ToTable("story_outlines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Mode).HasConversion<int>();
            entity.Property(e => e.BranchTopic).HasColumnType("text");
            entity.Property(e => e.ContinuationAnchor).HasColumnType("text");
            entity.Property(e => e.DivergencePolicy).HasConversion<int>();
            entity.Property(e => e.OutlineSummary).HasColumnType("text");
            entity.HasIndex(e => e.StoryProjectId);
            entity.HasIndex(e => new { e.StoryProjectId, e.IsDefault })
                  .IsUnique()
                  .HasFilter("\"IsDefault\" = TRUE");
            entity.HasOne<StoryProject>().WithMany().HasForeignKey(e => e.StoryProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Novel>().WithMany().HasForeignKey(e => e.SourceNovelId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.Property(e => e.ChainId).IsRequired(false);
            entity.Property(e => e.PreviousOutlineId).IsRequired(false);
            entity.HasOne<OutlineChain>().WithMany().HasForeignKey(e => e.ChainId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ── OutlineChain ─────────────────────────────────────────────────────
        modelBuilder.Entity<OutlineChain>(entity =>
        {
            entity.ToTable("outline_chains");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Mode).HasConversion<int>();
            entity.HasIndex(e => e.StoryProjectId);
            entity.HasOne<StoryProject>().WithMany().HasForeignKey(e => e.StoryProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Chapter ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Chapter>(entity =>
        {
            entity.ToTable("chapters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.AllowedRevealLevel).HasConversion<int>();
            entity.Property(e => e.Goal).HasColumnType("text");
            entity.Property(e => e.Summary).HasColumnType("text");
            entity.Property(e => e.DraftText).HasColumnType("text");
            entity.Property(e => e.FinalText).HasColumnType("text");
            entity.Property(e => e.Conflict).HasColumnType("text");
            entity.Property(e => e.EmotionCurve).HasColumnType("text");
            entity.Property(e => e.KeyCharacterIds).HasColumnType("uuid[]").IsRequired(false);
            entity.Property(e => e.MustIncludePoints).HasColumnType("text[]").IsRequired(false);
            entity.Property(e => e.SourceSuggestionId);
            entity.HasIndex(e => e.StoryOutlineId);
            entity.HasIndex(e => e.SourceSuggestionId);
            entity.HasIndex(e => e.StoryProjectId);
            entity.HasIndex(e => new { e.StoryOutlineId, e.Number }).IsUnique();
            entity.HasOne<StoryProject>().WithMany().HasForeignKey(e => e.StoryProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<StoryOutline>().WithMany().HasForeignKey(e => e.StoryOutlineId)
                  .OnDelete(DeleteBehavior.Cascade);
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
            entity.HasOne<Chapter>().WithMany().HasForeignKey(e => e.ChapterId)
                  .OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(e => e.SourceNovelId).IsRequired(false);
            // StoryOutlineId 可为 null：null 表示「原著角色池」（项目级只读参考）
            entity.Property(e => e.StoryOutlineId).IsRequired(false);
            entity.HasIndex(e => e.StoryProjectId);
            entity.HasIndex(e => e.StoryOutlineId);
            entity.HasOne<StoryProject>().WithMany().HasForeignKey(e => e.StoryProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<StoryOutline>().WithMany().HasForeignKey(e => e.StoryOutlineId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Cascade);
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
            entity.HasOne<StoryProject>().WithMany().HasForeignKey(e => e.StoryProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── WorldRule ────────────────────────────────────────────────────────
        modelBuilder.Entity<WorldRule>(entity =>
        {
            entity.ToTable("world_rules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(200);
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.SourceNovelId).IsRequired(false);
            entity.HasIndex(e => e.StoryProjectId);
            entity.HasOne<StoryProject>().WithMany().HasForeignKey(e => e.StoryProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
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
            entity.HasOne<StoryProject>().WithMany().HasForeignKey(e => e.StoryProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(e => e.EndingSummary).HasColumnType("text");
            entity.Property(e => e.StyleSummary).HasColumnType("text");
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasIndex(e => e.StoryProjectId);
            entity.HasIndex(e => new { e.StoryProjectId, e.FileHash })
                  .HasFilter("\"FileHash\" IS NOT NULL");
            entity.HasOne<StoryProject>().WithMany().HasForeignKey(e => e.StoryProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
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

        // ── NovelCharacterSnapshot ────────────────────────────────────────────
        modelBuilder.Entity<NovelCharacterSnapshot>(entity =>
        {
            entity.ToTable("novel_character_snapshots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CharacterName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.EndingState).HasColumnType("text").IsRequired();
            entity.HasIndex(e => e.NovelId);
            entity.HasIndex(e => e.StoryProjectId);
            entity.HasOne<Novel>().WithMany().HasForeignKey(e => e.NovelId)
                  .OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(e => e.InputFull).HasColumnType("text");
            entity.Property(e => e.OutputFull).HasColumnType("text");
            entity.Property(e => e.ErrorMessage).HasColumnType("text");
            entity.HasIndex(e => e.AgentName);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.StartedAt);
        });

        // ── AgentSuggestion ───────────────────────────────────────────────────
        modelBuilder.Entity<AgentSuggestion>(entity =>
        {
            entity.ToTable("agent_suggestions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Category).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ContentJson).HasColumnType("text").IsRequired();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.SourceNovelId).IsRequired(false);
            entity.HasIndex(e => e.AgentRunId);
            entity.HasIndex(e => e.StoryProjectId);
            entity.HasIndex(e => new { e.StoryProjectId, e.Status });
            entity.HasIndex(e => e.SourceNovelId);
            entity.HasOne<StoryProject>().WithMany().HasForeignKey(e => e.StoryProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── PlotThread ────────────────────────────────────────────────────────
        modelBuilder.Entity<PlotThread>(entity =>
        {
            entity.ToTable("plot_threads");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.Importance).HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.RelatedCharacterIds).HasColumnType("uuid[]").IsRequired(false);
            entity.Property(e => e.Tags).HasMaxLength(500);
            entity.Property(e => e.Visibility).HasConversion<int>().HasDefaultValue(PlotThreadVisibility.Chain).HasSentinel(PlotThreadVisibility.Chain);
            entity.HasIndex(e => e.StoryProjectId);
            entity.HasIndex(e => new { e.StoryProjectId, e.Status });
            entity.HasIndex(e => new { e.StoryProjectId, e.ChainId });
            entity.HasOne<StoryProject>().WithMany().HasForeignKey(e => e.StoryProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ChapterEvent (Module D 正典事实层 / 时间线) ──────────────────────
        modelBuilder.Entity<ChapterEvent>(entity =>
        {
            entity.ToTable("chapter_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).HasMaxLength(60).IsRequired();
            entity.Property(e => e.EventText).HasColumnType("text").IsRequired();
            entity.Property(e => e.ActorCharacterIds).HasColumnType("uuid[]").IsRequired(false);
            entity.Property(e => e.TargetCharacterIds).HasColumnType("uuid[]").IsRequired(false);
            entity.Property(e => e.Location).HasMaxLength(200);
            entity.Property(e => e.TimePoint).HasMaxLength(200);
            entity.Property(e => e.Importance).HasMaxLength(20);
            entity.HasIndex(e => e.StoryProjectId);
            entity.HasIndex(e => e.StoryOutlineId);
            entity.HasIndex(e => new { e.StoryProjectId, e.ChapterId });
            entity.HasIndex(e => new { e.StoryProjectId, e.IsIrreversible });
            entity.HasIndex(e => new { e.StoryOutlineId, e.ChapterId });
            entity.HasIndex(e => new { e.StoryOutlineId, e.IsIrreversible });
            entity.HasOne<StoryProject>().WithMany().HasForeignKey(e => e.StoryProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<StoryOutline>().WithMany().HasForeignKey(e => e.StoryOutlineId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Chapter>().WithMany().HasForeignKey(e => e.ChapterId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── CanonFact (Module D 正典事实层 / 状态真相) ───────────────────────
        modelBuilder.Entity<CanonFact>(entity =>
        {
            entity.ToTable("canon_facts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FactType).HasMaxLength(60).IsRequired();
            entity.Property(e => e.FactKey).HasMaxLength(300).IsRequired();
            entity.Property(e => e.FactValue).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Notes).HasColumnType("text");
            entity.HasIndex(e => e.StoryProjectId);
            entity.HasIndex(e => e.StoryOutlineId);
            entity.HasIndex(e => new { e.StoryProjectId, e.FactType });
            entity.HasIndex(e => new { e.StoryProjectId, e.IsLocked });
            entity.HasIndex(e => new { e.StoryOutlineId, e.FactType });
            entity.HasIndex(e => new { e.StoryOutlineId, e.FactType, e.FactKey }).IsUnique();
            entity.HasIndex(e => new { e.StoryOutlineId, e.IsLocked });
            entity.HasOne<StoryProject>().WithMany().HasForeignKey(e => e.StoryProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<StoryOutline>().WithMany().HasForeignKey(e => e.StoryOutlineId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── FeatureFlag ───────────────────────────────────────────────────────
        modelBuilder.Entity<FeatureFlag>(entity =>
        {
            entity.ToTable("feature_flags");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // ── BackgroundTaskRecord ──────────────────────────────────────────────
        modelBuilder.Entity<BackgroundTaskRecord>(entity =>
        {
            entity.ToTable("background_tasks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TaskType).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.Title).HasMaxLength(300).IsRequired();
            entity.Property(e => e.StatusMessage).HasMaxLength(500);
            entity.Property(e => e.ErrorMessage).HasColumnType("text");
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.Status });
        });

        // ── ChapterBatchDraftRun ──────────────────────────────────────────────
        modelBuilder.Entity<ChapterBatchDraftRun>(entity =>
        {
            entity.ToTable("chapter_batch_draft_runs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.StoryOutlineId).IsRequired();
            entity.Property(e => e.FailedChapterIds).HasColumnType("uuid[]").IsRequired(false);
            entity.Property(e => e.ErrorMessage).HasColumnType("text");
            entity.HasIndex(e => e.StoryProjectId);
            entity.HasIndex(e => e.StoryOutlineId);
            entity.HasIndex(e => new { e.StoryProjectId, e.Status });
            entity.HasIndex(e => new { e.StoryOutlineId, e.Status });
            entity.HasOne<StoryProject>().WithMany().HasForeignKey(e => e.StoryProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<StoryOutline>().WithMany().HasForeignKey(e => e.StoryOutlineId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
