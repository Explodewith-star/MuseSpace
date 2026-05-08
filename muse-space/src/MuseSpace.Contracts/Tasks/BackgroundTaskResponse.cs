namespace MuseSpace.Contracts.Tasks;

public sealed class BackgroundTaskResponse
{
    public Guid Id { get; init; }
    public string TaskType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Progress { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? StatusMessage { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
