using MuseSpace.Domain.Enums;

namespace MuseSpace.Domain.Entities;

public class Chapter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryProjectId { get; set; }
    public int Number { get; set; }
    public string? Title { get; set; }
    public string? Goal { get; set; }
    public ChapterStatus Status { get; set; } = ChapterStatus.Planned;
    public string? Summary { get; set; }
    public string? DraftText { get; set; }
    public string? FinalText { get; set; }

    /// <summary>核心冲突</summary>
    public string? Conflict { get; set; }

    /// <summary>情感曲线（如 平静→紧张→爆发→余韵）</summary>
    public string? EmotionCurve { get; set; }

    /// <summary>关键出场角色 Id 列表</summary>
    public List<Guid> KeyCharacterIds { get; set; } = new();

    /// <summary>本章必须命中的要点</summary>
    public List<string> MustIncludePoints { get; set; } = new();

    /// <summary>
    /// 导入来源的大纲建议 ID（由 OutlineSuggestionApplier 设置）。
    /// 用于在撤回/删除大纲时级联删除此章节。
    /// </summary>
    public Guid? SourceSuggestionId { get; set; }
}
