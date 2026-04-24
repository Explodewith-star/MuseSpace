namespace MuseSpace.Contracts.Novels;

public sealed class NovelResponse
{
    public Guid Id { get; init; }
    public Guid StoryProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public long? FileSize { get; init; }

    /// <summary>Pending / Processing / Indexed / Failed</summary>
    public string Status { get; init; } = string.Empty;

    public int TotalChunks { get; init; }
    public DateTime CreatedAt { get; init; }
}
