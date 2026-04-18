namespace MuseSpace.Contracts.Chapters;

public sealed class CreateChapterRequest
{
    public int Number { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public string? Goal { get; init; }
}
