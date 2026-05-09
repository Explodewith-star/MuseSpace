namespace MuseSpace.Contracts.CanonFacts;

public sealed class CanonFactResponse
{
    public Guid Id { get; set; }
    public Guid StoryProjectId { get; set; }
    public Guid StoryOutlineId { get; set; }
    public string FactType { get; set; } = string.Empty;
    public Guid? SubjectId { get; set; }
    public Guid? ObjectId { get; set; }
    public string FactKey { get; set; } = string.Empty;
    public string FactValue { get; set; } = string.Empty;
    public Guid? SourceChapterId { get; set; }
    public double Confidence { get; set; }
    public bool IsLocked { get; set; }
    public Guid? InvalidatedByChapterId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class UpsertCanonFactRequest
{
    public Guid? StoryOutlineId { get; set; }
    public string FactType { get; set; } = string.Empty;
    public Guid? SubjectId { get; set; }
    public Guid? ObjectId { get; set; }
    public string FactKey { get; set; } = string.Empty;
    public string FactValue { get; set; } = string.Empty;
    public Guid? SourceChapterId { get; set; }
    public double? Confidence { get; set; }
    public bool? IsLocked { get; set; }
    public Guid? InvalidatedByChapterId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>PATCH 用：仅更新指定字段（锁定 / 解锁 / 修正取值）。</summary>
public sealed class PatchCanonFactRequest
{
    public string? FactValue { get; set; }
    public bool? IsLocked { get; set; }
    public Guid? InvalidatedByChapterId { get; set; }
    public string? Notes { get; set; }
}
