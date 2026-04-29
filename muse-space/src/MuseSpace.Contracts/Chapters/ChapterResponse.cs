namespace MuseSpace.Contracts.Chapters;

public sealed class ChapterResponse
{
    public Guid Id { get; init; }
    public Guid StoryProjectId { get; init; }
    public int Number { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public string? Goal { get; init; }
    public int Status { get; init; }
    public string? DraftText { get; init; }
    public string? FinalText { get; init; }
    public string? Conflict { get; init; }
    public string? EmotionCurve { get; init; }
    public List<Guid> KeyCharacterIds { get; init; } = new();
    public List<string> MustIncludePoints { get; init; } = new();
    public Guid? SourceSuggestionId { get; init; }
}
