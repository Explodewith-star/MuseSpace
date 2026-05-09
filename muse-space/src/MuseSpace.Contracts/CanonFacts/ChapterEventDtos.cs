namespace MuseSpace.Contracts.CanonFacts;

public sealed class ChapterEventResponse
{
    public Guid Id { get; set; }
    public Guid StoryProjectId { get; set; }
    public Guid StoryOutlineId { get; set; }
    public Guid ChapterId { get; set; }
    public int Order { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventText { get; set; } = string.Empty;
    public List<Guid>? ActorCharacterIds { get; set; }
    public List<Guid>? TargetCharacterIds { get; set; }
    public string? Location { get; set; }
    public string? TimePoint { get; set; }
    public string? Importance { get; set; }
    public bool IsIrreversible { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class UpsertChapterEventRequest
{
    /// <summary>更新 / 替换时可选；为空时由后端生成。</summary>
    public Guid? Id { get; set; }
    public int Order { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventText { get; set; } = string.Empty;
    public List<Guid>? ActorCharacterIds { get; set; }
    public List<Guid>? TargetCharacterIds { get; set; }
    public string? Location { get; set; }
    public string? TimePoint { get; set; }
    public string? Importance { get; set; }
    public bool IsIrreversible { get; set; }
}

/// <summary>批量替换某章事件（PUT /api/projects/.../chapters/{id}/events）。</summary>
public sealed class ReplaceChapterEventsRequest
{
    public List<UpsertChapterEventRequest> Events { get; set; } = new();
}
