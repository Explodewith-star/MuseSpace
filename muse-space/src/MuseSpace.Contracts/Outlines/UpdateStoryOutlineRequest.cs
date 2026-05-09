namespace MuseSpace.Contracts.Outlines;

public sealed class UpdateStoryOutlineRequest
{
    public string? Name { get; init; }
    public string? Mode { get; init; }
    public Guid? SourceNovelId { get; init; }
    public int? SourceRangeStart { get; init; }
    public int? SourceRangeEnd { get; init; }
    public string? BranchTopic { get; init; }
    public string? ContinuationAnchor { get; init; }
    public string? DivergencePolicy { get; init; }
    public int? TargetChapterCount { get; init; }
    public string? OutlineSummary { get; init; }
}
