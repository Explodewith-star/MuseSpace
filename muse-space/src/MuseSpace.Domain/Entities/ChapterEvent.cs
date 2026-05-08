namespace MuseSpace.Domain.Entities;

/// <summary>
/// 章节事件（Module D 正典事实层 / 第一层：时间线）。
/// 一行 ChapterEvent 表示某一章发生的一件可被结构化引用的事件。
/// 由 ChapterEventExtractionJob 自动抽取，亦可由作者手动新增 / 修改 / 删除。
/// </summary>
public class ChapterEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryProjectId { get; set; }
    public Guid ChapterId { get; set; }

    /// <summary>事件在本章内的顺序（用于稳定排序）。</summary>
    public int Order { get; set; }

    /// <summary>事件类型：Proposal / Reveal / Death / Battle / Reconcile / Awakening / Custom 等自由文本。</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>事件正文（≈1-2 句客观陈述，避免主观评论）。</summary>
    public string EventText { get; set; } = string.Empty;

    /// <summary>动作发起者角色 ID（PostgreSQL uuid[]）。</summary>
    public List<Guid>? ActorCharacterIds { get; set; }

    /// <summary>动作承受者 / 目标角色 ID。</summary>
    public List<Guid>? TargetCharacterIds { get; set; }

    /// <summary>事件发生地点。</summary>
    public string? Location { get; set; }

    /// <summary>事件时间点（自由文本，如"中元节当夜"）。</summary>
    public string? TimePoint { get; set; }

    /// <summary>重要度：High / Medium / Low。</summary>
    public string? Importance { get; set; }

    /// <summary>
    /// 是否为不可重复事件（如求婚、退婚、决斗、第一次告白）。
    /// 标记为 true 后，<c>DuplicateEventCheckJob</c> 将禁止后续章节再次发生同 EventType 的事件。
    /// </summary>
    public bool IsIrreversible { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
