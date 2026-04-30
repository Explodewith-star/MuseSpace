using MuseSpace.Domain.Enums;

namespace MuseSpace.Domain.Entities;

/// <summary>
/// 伏笔 / 故事线索实体（D4-C 伏笔追踪）。
/// 一条 PlotThread 表示作者埋设的一条剧情线索，包含埋设章节、回收章节、状态和重要度。
/// 由 PlotThreadTrackingAgent 自动发现新埋伏 / 标记已回收，亦可由作者手动维护。
/// </summary>
public class PlotThread
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryProjectId { get; set; }

    /// <summary>线索短标题，如"主角的玉佩之谜"。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>详细描述（埋伏内容、计划如何推进、计划在哪一卷收回等）。</summary>
    public string? Description { get; set; }

    /// <summary>重要度：High / Medium / Low。仅作前端排序提示用。</summary>
    public string? Importance { get; set; }

    /// <summary>当前状态：Introduced / Active / PaidOff / Abandoned。</summary>
    public ForeshadowingStatus Status { get; set; } = ForeshadowingStatus.Introduced;

    /// <summary>埋设章节（首次出现）。</summary>
    public Guid? PlantedInChapterId { get; set; }

    /// <summary>回收章节（PaidOff 状态时填写）。</summary>
    public Guid? ResolvedInChapterId { get; set; }

    /// <summary>关联角色 ID（PostgreSQL uuid[]）。</summary>
    public List<Guid>? RelatedCharacterIds { get; set; }

    /// <summary>逗号分隔的标签，如"修真,长卷"。</summary>
    public string? Tags { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
