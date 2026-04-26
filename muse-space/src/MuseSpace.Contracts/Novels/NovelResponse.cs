namespace MuseSpace.Contracts.Novels;

public sealed class NovelResponse
{
    public Guid Id { get; init; }
    public Guid StoryProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public long? FileSize { get; init; }

    /// <summary>Pending / Chunking / Embedding / Indexed / Failed</summary>
    public string Status { get; init; } = string.Empty;

    public int TotalChunks { get; init; }
    public int ProgressDone { get; init; }
    public int ProgressTotal { get; init; }
    public string? LastError { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
