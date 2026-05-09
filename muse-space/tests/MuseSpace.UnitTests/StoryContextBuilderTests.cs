using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MuseSpace.Application.Abstractions.Memory;
using MuseSpace.Application.Abstractions.Story;
using MuseSpace.Domain.Entities;
using MuseSpace.Infrastructure.Persistence;
using MuseSpace.Infrastructure.Persistence.Entities;
using MuseSpace.Infrastructure.Persistence.Repositories;
using MuseSpace.Infrastructure.Story;

namespace MuseSpace.UnitTests;

file sealed class StoryContextTestDbContext : MuseSpaceDbContext
{
    public StoryContextTestDbContext(DbContextOptions<MuseSpaceDbContext> opts) : base(opts) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<NovelChunkEmbedding>().Ignore(e => e.Embedding);
    }
}

public sealed class StoryContextBuilderTests
{
    [Fact]
    public async Task BuildAsync_OriginalDraft_DoesNotRetrieveNovelSnippetsByDefault()
    {
        await using var db = NewDb();
        var projectId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();

        db.StoryProjects.Add(new StoryProject
        {
            Id = projectId,
            Name = "平凡校园",
            Description = "高中生日常里的轻微异常",
        });
        var outlineId = Guid.NewGuid();
        db.StoryOutlines.Add(new StoryOutline
        {
            Id = outlineId,
            StoryProjectId = projectId,
            Name = "原创主线",
            IsDefault = true,
        });
        db.Chapters.Add(new Chapter
        {
            Id = chapterId,
            StoryProjectId = projectId,
            StoryOutlineId = outlineId,
            Number = 1,
            Title = "最后一节课",
            Summary = "校园广播出现刺耳杂音。",
        });
        await db.SaveChangesAsync();

        var search = new RecordingNovelMemorySearchService();
        search.ProjectResults.Add(new NovelChunkSearchResult
        {
            ChunkId = Guid.NewGuid(),
            Content = "废弃工厂、鬼婴、实验室倒计时。",
            Similarity = 0.99,
        });

        var context = await CreateBuilder(db, search).BuildAsync(new StoryContextRequest
        {
            StoryProjectId = projectId,
            ChapterId = chapterId,
            SceneGoal = "杨间和张伟在宿舍打闹，天空突然变暗。",
        });

        Assert.Empty(context.NovelContextSnippets);
        Assert.Equal(0, search.ProjectSearchCalls);
        Assert.Equal(0, search.NovelSearchCalls);
    }

    [Fact]
    public async Task BuildAsync_WhenNovelContextExplicitlyEnabled_RetrievesProjectSnippets()
    {
        await using var db = NewDb();
        var projectId = Guid.NewGuid();

        db.StoryProjects.Add(new StoryProject
        {
            Id = projectId,
            Name = "续写实验",
            Description = "带导入原著的项目",
        });
        db.StoryOutlines.Add(new StoryOutline
        {
            Id = Guid.NewGuid(),
            StoryProjectId = projectId,
            Name = "原创主线",
            IsDefault = true,
        });
        await db.SaveChangesAsync();

        var search = new RecordingNovelMemorySearchService();
        search.ProjectResults.Add(new NovelChunkSearchResult
        {
            ChunkId = Guid.NewGuid(),
            Content = "可用的原著参考片段",
            Similarity = 0.92,
        });

        var context = await CreateBuilder(db, search).BuildAsync(new StoryContextRequest
        {
            StoryProjectId = projectId,
            SceneGoal = "生成需要显式参考原著的片段。",
            IncludeNovelContext = true,
        });

        Assert.Single(context.NovelContextSnippets);
        Assert.Equal(1, search.ProjectSearchCalls);
    }

    [Fact]
    public async Task BuildAsync_FiltersSummariesEventsAndFactsToPriorChaptersOnly()
    {
        await using var db = NewDb();
        var projectId = Guid.NewGuid();
        var chapter1Id = Guid.NewGuid();
        var chapter2Id = Guid.NewGuid();
        var chapter3Id = Guid.NewGuid();

        db.StoryProjects.Add(new StoryProject
        {
            Id = projectId,
            Name = "时间线项目",
            Description = "验证当前章节只能看到过去。",
        });
        var outlineId = Guid.NewGuid();
        db.StoryOutlines.Add(new StoryOutline
        {
            Id = outlineId,
            StoryProjectId = projectId,
            Name = "原创主线",
            IsDefault = true,
        });
        db.Chapters.AddRange(
            new Chapter
            {
                Id = chapter1Id,
                StoryProjectId = projectId,
                StoryOutlineId = outlineId,
                Number = 1,
                Title = "前兆",
                Summary = "手机信号中断，校园广播响起杂音。",
            },
            new Chapter
            {
                Id = chapter2Id,
                StoryProjectId = projectId,
                StoryOutlineId = outlineId,
                Number = 2,
                Title = "最后一节课",
                Summary = "当前章应保持校园日常与异变前兆。",
            },
            new Chapter
            {
                Id = chapter3Id,
                StoryProjectId = projectId,
                StoryOutlineId = outlineId,
                Number = 3,
                Title = "实验室",
                Summary = "未来才会进入废弃工厂和实验室。",
            });
        db.ChapterEvents.AddRange(
            new ChapterEvent
            {
                StoryProjectId = projectId,
                StoryOutlineId = outlineId,
                ChapterId = chapter1Id,
                Order = 1,
                EventType = "Signal",
                EventText = "手机信号中断。",
                IsIrreversible = true,
            },
            new ChapterEvent
            {
                StoryProjectId = projectId,
                StoryOutlineId = outlineId,
                ChapterId = chapter3Id,
                Order = 1,
                EventType = "Factory",
                EventText = "张伟进入废弃工厂实验室。",
                IsIrreversible = true,
            });
        db.CanonFacts.AddRange(
            new CanonFact
            {
                StoryProjectId = projectId,
                StoryOutlineId = outlineId,
                FactType = "Relationship",
                FactKey = "杨间-张伟",
                FactValue = "普通同学",
                IsLocked = true,
                SourceChapterId = chapter1Id,
            },
            new CanonFact
            {
                StoryProjectId = projectId,
                StoryOutlineId = outlineId,
                FactType = "LifeStatus",
                FactKey = "张伟",
                FactValue = "被困实验室",
                IsLocked = true,
                SourceChapterId = chapter3Id,
            },
            new CanonFact
            {
                StoryProjectId = projectId,
                StoryOutlineId = outlineId,
                FactType = "UniqueEvent",
                FactKey = "手机信号异常",
                FactValue = "已发生",
                IsLocked = true,
                SourceChapterId = chapter1Id,
            },
            new CanonFact
            {
                StoryProjectId = projectId,
                StoryOutlineId = outlineId,
                FactType = "UniqueEvent",
                FactKey = "废弃工厂倒计时",
                FactValue = "已发生",
                IsLocked = true,
                SourceChapterId = chapter3Id,
            });
        await db.SaveChangesAsync();

        var context = await CreateBuilder(db, new RecordingNovelMemorySearchService()).BuildAsync(
            new StoryContextRequest
            {
                StoryProjectId = projectId,
                ChapterId = chapter2Id,
                SceneGoal = "当前章只写校园日常里的异常前兆。",
            });

        Assert.Contains(context.RecentChapterSummaries, x => x.Contains("手机信号中断"));
        Assert.DoesNotContain(context.RecentChapterSummaries, x => x.Contains("废弃工厂"));
        Assert.Contains(context.RecentEvents, x => x.Contains("手机信号中断"));
        Assert.DoesNotContain(context.RecentEvents, x => x.Contains("废弃工厂"));
        Assert.Contains(context.CharacterStateFacts, x => x.Contains("普通同学"));
        Assert.DoesNotContain(context.CharacterStateFacts, x => x.Contains("被困实验室"));
        Assert.Contains(context.ImmutableFacts, x => x.Contains("手机信号异常"));
        Assert.DoesNotContain(context.ImmutableFacts, x => x.Contains("废弃工厂"));
    }

    [Fact]
    public async Task BuildAsync_UsesMostRecentPriorEventChaptersInsteadOfProjectLatest()
    {
        await using var db = NewDb();
        var projectId = Guid.NewGuid();
        var chapterIds = Enumerable.Range(1, 5).Select(_ => Guid.NewGuid()).ToArray();

        db.StoryProjects.Add(new StoryProject
        {
            Id = projectId,
            Name = "长篇项目",
            Description = "验证早期章节不会被最新章节窗口挤掉。",
        });
        var outlineId = Guid.NewGuid();
        db.StoryOutlines.Add(new StoryOutline
        {
            Id = outlineId,
            StoryProjectId = projectId,
            Name = "原创主线",
            IsDefault = true,
        });
        db.Chapters.AddRange(chapterIds.Select((id, index) => new Chapter
        {
            Id = id,
            StoryProjectId = projectId,
            StoryOutlineId = outlineId,
            Number = index + 1,
            Title = $"第 {index + 1} 章",
            Summary = $"第 {index + 1} 章摘要",
        }));
        db.ChapterEvents.AddRange(
            new ChapterEvent
            {
                StoryProjectId = projectId,
                StoryOutlineId = outlineId,
                ChapterId = chapterIds[0],
                Order = 1,
                EventType = "Past",
                EventText = "第一章已经发生的事件。",
            },
            new ChapterEvent
            {
                StoryProjectId = projectId,
                StoryOutlineId = outlineId,
                ChapterId = chapterIds[1],
                Order = 1,
                EventType = "Past",
                EventText = "第二章已经发生的事件。",
            },
            new ChapterEvent
            {
                StoryProjectId = projectId,
                StoryOutlineId = outlineId,
                ChapterId = chapterIds[3],
                Order = 1,
                EventType = "Future",
                EventText = "第四章未来事件。",
            },
            new ChapterEvent
            {
                StoryProjectId = projectId,
                StoryOutlineId = outlineId,
                ChapterId = chapterIds[4],
                Order = 1,
                EventType = "Future",
                EventText = "第五章未来事件。",
            });
        await db.SaveChangesAsync();

        var context = await CreateBuilder(db, new RecordingNovelMemorySearchService()).BuildAsync(
            new StoryContextRequest
            {
                StoryProjectId = projectId,
                ChapterId = chapterIds[2],
                SceneGoal = "第三章生成时只读取第一、二章事件。",
            });

        Assert.Contains(context.RecentEvents, x => x.Contains("第一章已经发生"));
        Assert.Contains(context.RecentEvents, x => x.Contains("第二章已经发生"));
        Assert.DoesNotContain(context.RecentEvents, x => x.Contains("第四章未来"));
        Assert.DoesNotContain(context.RecentEvents, x => x.Contains("第五章未来"));
    }

    [Fact]
    public async Task BuildAsync_FiltersFactsAndEventsToCurrentOutline()
    {
        await using var db = NewDb();
        var projectId = Guid.NewGuid();
        var mainOutlineId = Guid.NewGuid();
        var sideOutlineId = Guid.NewGuid();
        var mainChapter1Id = Guid.NewGuid();
        var mainChapter2Id = Guid.NewGuid();
        var sideChapter1Id = Guid.NewGuid();

        db.StoryProjects.Add(new StoryProject
        {
            Id = projectId,
            Name = "多大纲项目",
            Description = "验证事实和事件不会跨大纲污染。",
        });
        db.StoryOutlines.AddRange(
            new StoryOutline
            {
                Id = mainOutlineId,
                StoryProjectId = projectId,
                Name = "原创主线",
                IsDefault = true,
            },
            new StoryOutline
            {
                Id = sideOutlineId,
                StoryProjectId = projectId,
                Name = "番外线",
            });
        db.Chapters.AddRange(
            new Chapter
            {
                Id = mainChapter1Id,
                StoryProjectId = projectId,
                StoryOutlineId = mainOutlineId,
                Number = 1,
                Title = "主线前情",
                Summary = "杨间和张伟仍是普通同学。",
            },
            new Chapter
            {
                Id = mainChapter2Id,
                StoryProjectId = projectId,
                StoryOutlineId = mainOutlineId,
                Number = 2,
                Title = "主线当前",
                Summary = "继续校园日常。",
            },
            new Chapter
            {
                Id = sideChapter1Id,
                StoryProjectId = projectId,
                StoryOutlineId = sideOutlineId,
                Number = 1,
                Title = "番外前情",
                Summary = "番外线里张伟已经被困实验室。",
            });
        db.ChapterEvents.AddRange(
            new ChapterEvent
            {
                StoryProjectId = projectId,
                StoryOutlineId = mainOutlineId,
                ChapterId = mainChapter1Id,
                Order = 1,
                EventType = "Classroom",
                EventText = "两人一起回到教室。",
            },
            new ChapterEvent
            {
                StoryProjectId = projectId,
                StoryOutlineId = sideOutlineId,
                ChapterId = sideChapter1Id,
                Order = 1,
                EventType = "Lab",
                EventText = "张伟在番外线进入实验室。",
            });
        db.CanonFacts.AddRange(
            new CanonFact
            {
                StoryProjectId = projectId,
                StoryOutlineId = mainOutlineId,
                FactType = "Relationship",
                FactKey = "杨间-张伟",
                FactValue = "普通同学",
                IsLocked = true,
                SourceChapterId = mainChapter1Id,
            },
            new CanonFact
            {
                StoryProjectId = projectId,
                StoryOutlineId = sideOutlineId,
                FactType = "Relationship",
                FactKey = "杨间-张伟",
                FactValue = "番外线敌对",
                IsLocked = true,
                SourceChapterId = sideChapter1Id,
            });
        await db.SaveChangesAsync();

        var context = await CreateBuilder(db, new RecordingNovelMemorySearchService()).BuildAsync(
            new StoryContextRequest
            {
                StoryProjectId = projectId,
                ChapterId = mainChapter2Id,
                SceneGoal = "主线第二章继续推进。",
            });

        Assert.Contains(context.RecentEvents, x => x.Contains("回到教室"));
        Assert.DoesNotContain(context.RecentEvents, x => x.Contains("实验室"));
        Assert.Contains(context.CharacterStateFacts, x => x.Contains("普通同学"));
        Assert.DoesNotContain(context.CharacterStateFacts, x => x.Contains("番外线敌对"));
    }

    private static MuseSpaceDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<MuseSpaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new StoryContextTestDbContext(opts);
    }

    private static StoryContextBuilder CreateBuilder(
        MuseSpaceDbContext db,
        INovelMemorySearchService searchService)
        => new(
            new EfStoryProjectRepository(db),
            new EfCharacterRepository(db),
            new EfWorldRuleRepository(db),
            new EfChapterRepository(db),
            new EfStoryOutlineRepository(db),
            new EfStyleProfileRepository(db),
            searchService,
            new EfChapterEventRepository(db),
            new EfCanonFactRepository(db),
            new EfNovelChunkRepository(db),
            new EfNovelRepository(db),
            new EfNovelCharacterSnapshotRepository(db),
            NullLogger<StoryContextBuilder>.Instance);

    private sealed class RecordingNovelMemorySearchService : INovelMemorySearchService
    {
        public List<NovelChunkSearchResult> ProjectResults { get; } = [];
        public List<NovelChunkSearchResult> NovelResults { get; } = [];
        public int ProjectSearchCalls { get; private set; }
        public int NovelSearchCalls { get; private set; }

        public Task<IReadOnlyList<NovelChunkSearchResult>> SearchAsync(
            Guid projectId,
            string queryText,
            int topK = 5,
            CancellationToken ct = default)
        {
            ProjectSearchCalls++;
            return Task.FromResult<IReadOnlyList<NovelChunkSearchResult>>(ProjectResults);
        }

        public Task<IReadOnlyList<NovelChunkSearchResult>> SearchByNovelAsync(
            Guid novelId,
            string queryText,
            int topK = 5,
            CancellationToken ct = default)
        {
            NovelSearchCalls++;
            return Task.FromResult<IReadOnlyList<NovelChunkSearchResult>>(NovelResults);
        }
    }
}
