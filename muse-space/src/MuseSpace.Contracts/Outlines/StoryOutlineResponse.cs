namespace MuseSpace.Contracts.Outlines;

public sealed class StoryOutlineResponse
{
    public Guid Id { get; init; }
    public Guid StoryProjectId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Mode { get; init; } = "Original";
    public Guid? SourceNovelId { get; init; }
    public int? SourceRangeStart { get; init; }
    public int? SourceRangeEnd { get; init; }
    public string? BranchTopic { get; init; }
    public string? ContinuationAnchor { get; init; }
    public string DivergencePolicy { get; init; } = "SoftCanon";
    public int? TargetChapterCount { get; init; }
    public string? OutlineSummary { get; init; }
    public bool IsDefault { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public int ChapterCount { get; init; }
}
