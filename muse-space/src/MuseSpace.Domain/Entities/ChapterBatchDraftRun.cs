namespace MuseSpace.Domain.Entities;

/// <summary>
/// 章节批量草稿生成任务的运行记录。每次"批量生成 N 章"创建一行。
/// 顺序执行，单章 5 分钟超时，单批 30 分钟超时；可中止；失败容忍（单章失败不中断后续）。
/// </summary>
public class ChapterBatchDraftRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryProjectId { get; set; }
    public Guid StoryOutlineId { get; set; }
    public Guid? UserId { get; set; }

    /// <summary>起始章节号（含）。</summary>
    public int FromNumber { get; set; }

    /// <summary>结束章节号（含）。</summary>
    public int ToNumber { get; set; }

    /// <summary>是否跳过已有草稿的章节。</summary>
    public bool SkipChaptersWithDraft { get; set; }

    /// <summary>是否在生成草稿前自动调用 ChapterAutoPlanJob 填充写作计划。默认 true。</summary>
    public bool AutoFillPlan { get; set; } = true;

    /// <summary>批次包含的章节总数。</summary>
    public int TotalCount { get; set; }

    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }

    /// <summary>失败章节 ID 列表（uuid[]）。</summary>
    public List<Guid> FailedChapterIds { get; set; } = new();

    /// <summary>当前正在处理的章节 ID（运行中可见）。</summary>
    public Guid? CurrentChapterId { get; set; }

    public ChapterBatchDraftStatus Status { get; set; } = ChapterBatchDraftStatus.Pending;

    /// <summary>用户请求中止；运行中的章节完成后会停止后续。</summary>
    public bool CancelRequested { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    /// <summary>整体错误信息（仅在 Status=Failed 时填充）。</summary>
    public string? ErrorMessage { get; set; }
}

public enum ChapterBatchDraftStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    PartiallyFailed = 3,
    Cancelled = 4,
    Failed = 5,
}
