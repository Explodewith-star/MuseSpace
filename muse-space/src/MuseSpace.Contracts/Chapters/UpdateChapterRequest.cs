namespace MuseSpace.Contracts.Chapters;

public sealed class UpdateChapterRequest
{
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public string? Goal { get; init; }
    public string? DraftText { get; init; }
    public string? FinalText { get; init; }
    public int? Status { get; init; }
    public string? Conflict { get; init; }
    public string? EmotionCurve { get; init; }
    public List<Guid>? KeyCharacterIds { get; init; }
    public List<string>? MustIncludePoints { get; init; }
}
