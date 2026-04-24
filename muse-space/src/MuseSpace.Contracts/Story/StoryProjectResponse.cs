namespace MuseSpace.Contracts.Story;

public sealed class StoryProjectResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Genre { get; init; }
    public string? NarrativePerspective { get; init; }
    public Guid? UserId { get; init; }
    public DateTime CreatedAt { get; init; }
}
