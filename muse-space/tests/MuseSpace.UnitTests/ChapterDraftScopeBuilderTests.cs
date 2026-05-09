using MuseSpace.Contracts.Chapters;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;
using MuseSpace.Infrastructure.Jobs.Internal;

namespace MuseSpace.UnitTests;

public sealed class ChapterDraftScopeBuilderTests
{
    [Fact]
    public void Build_UsesOutlineModeAndFutureBeatsFromSameOutline()
    {
        var projectId = Guid.NewGuid();
        var outline = new StoryOutline
        {
            Id = Guid.NewGuid(),
            StoryProjectId = projectId,
            Name = "番外线",
            Mode = GenerationMode.SideStoryFromOriginal,
            DivergencePolicy = DivergencePolicy.StrictCanon,
            BranchTopic = "旧城支线",
        };
        var current = new Chapter
        {
            Id = Guid.NewGuid(),
            StoryProjectId = projectId,
            StoryOutlineId = outline.Id,
            Number = 1,
            Title = "雨夜",
            Goal = "只写旧城雨夜的前兆",
            Summary = "广播出现杂音，角色察觉异常。",
            MustIncludePoints = ["广播杂音"],
        };
        var future = new Chapter
        {
            Id = Guid.NewGuid(),
            StoryProjectId = projectId,
            StoryOutlineId = outline.Id,
            Number = 2,
            Title = "门后",
            Summary = "角色进入门后世界。",
        };

        var scope = ChapterDraftScopeBuilder.Build(
            projectId,
            current,
            outline,
            [current, future],
            new GenerateChapterDraftRequest { BranchTopic = "请求里的主题不应覆盖大纲主题" });

        Assert.Equal(outline.Id, scope.OutlineId);
        Assert.Equal(GenerationMode.SideStoryFromOriginal, scope.GenerationMode);
        Assert.Equal(DivergencePolicy.StrictCanon, scope.DivergencePolicy);
        Assert.Equal("旧城支线", scope.BranchTopic);
        Assert.Contains("门后", string.Join("\n", scope.ReservedFutureBeats));
        Assert.Contains("后续章节保留", scope.BoundaryInstruction);
        Assert.DoesNotContain("门后", scope.BoundaryInstruction);
    }

    [Fact]
    public void Build_InfersForeshadowForFirstChapterAndAllowsManualOverride()
    {
        var projectId = Guid.NewGuid();
        var outline = new StoryOutline
        {
            Id = Guid.NewGuid(),
            StoryProjectId = projectId,
            Name = "原创主线",
            Mode = GenerationMode.Original,
        };
        var chapter = new Chapter
        {
            Id = Guid.NewGuid(),
            StoryProjectId = projectId,
            StoryOutlineId = outline.Id,
            Number = 1,
            Title = "最后一节课",
            Summary = "天空突然变暗，手机信号中断。",
        };

        var inferred = ChapterDraftScopeBuilder.Build(projectId, chapter, outline, [chapter], null);
        Assert.Equal(ChapterRevealLevel.ForeshadowOnly, inferred.AllowedRevealLevel);

        chapter.AllowedRevealLevel = ChapterRevealLevel.DirectAnomaly;
        var overridden = ChapterDraftScopeBuilder.Build(projectId, chapter, outline, [chapter], null);
        Assert.Equal(ChapterRevealLevel.DirectAnomaly, overridden.AllowedRevealLevel);
    }

    [Fact]
    public void Build_InfersForeshadowForOminousFirstChapter()
    {
        var projectId = Guid.NewGuid();
        var outline = new StoryOutline
        {
            Id = Guid.NewGuid(),
            StoryProjectId = projectId,
            Name = "原创主线",
            Mode = GenerationMode.Original,
        };
        var chapter = new Chapter
        {
            Id = Guid.NewGuid(),
            StoryProjectId = projectId,
            StoryOutlineId = outline.Id,
            Number = 1,
            Title = "最后一节课",
            Goal = "写出第一个灵异前兆，但不要确认来源。",
            Summary = "广播杂音中夹着诡异身影的错觉，手机信号中断，学生开始不安。",
        };

        var scope = ChapterDraftScopeBuilder.Build(projectId, chapter, outline, [chapter], null);

        Assert.Equal(ChapterRevealLevel.ForeshadowOnly, scope.AllowedRevealLevel);
    }

    [Fact]
    public void Build_InfersDirectAnomalyForExplicitEncounter()
    {
        var projectId = Guid.NewGuid();
        var outline = new StoryOutline
        {
            Id = Guid.NewGuid(),
            StoryProjectId = projectId,
            Name = "原创主线",
            Mode = GenerationMode.Original,
        };
        var chapter = new Chapter
        {
            Id = Guid.NewGuid(),
            StoryProjectId = projectId,
            StoryOutlineId = outline.Id,
            Number = 2,
            Title = "看见鬼",
            Summary = "主角亲眼看见鬼出现，并确认异常真实存在。",
        };

        var scope = ChapterDraftScopeBuilder.Build(projectId, chapter, outline, [chapter], null);

        Assert.Equal(ChapterRevealLevel.DirectAnomaly, scope.AllowedRevealLevel);
    }
}
