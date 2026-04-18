namespace MuseSpace.Contracts.Story;

public sealed class CreateStoryProjectRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Genre { get; init; }
    public string? NarrativePerspective { get; init; }
}
