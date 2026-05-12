namespace MuseSpace.Contracts.Outlines;

public sealed class OutlineChainResponse
{
    public Guid Id { get; init; }
    public Guid StoryProjectId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Mode { get; init; } = "Original";
    public int DisplayOrder { get; init; }
    public DateTime CreatedAt { get; init; }
    public int OutlineCount { get; init; }
}
