namespace MuseSpace.Contracts.Chapters;

/// <summary>批量生成草稿任务的状态视图（前端轮询/SignalR 推送统一使用）。</summary>
public sealed class ChapterBatchDraftRunResponse
{
    public Guid Id { get; set; }
    public Guid StoryProjectId { get; set; }
    public Guid StoryOutlineId { get; set; }
    public int FromNumber { get; set; }
    public int ToNumber { get; set; }
    public int TotalCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<Guid> FailedChapterIds { get; set; } = new();
    public Guid? CurrentChapterId { get; set; }
    public string Status { get; set; } = "Pending";
    public bool CancelRequested { get; set; }
    public bool AutoFillPlan { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
