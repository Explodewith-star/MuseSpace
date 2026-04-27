namespace MuseSpace.Contracts.Suggestions;

public sealed class AgentSuggestionResponse
{
    public Guid Id { get; init; }
    public Guid AgentRunId { get; init; }
    public Guid StoryProjectId { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string ContentJson { get; init; } = "{}";
    public string Status { get; init; } = string.Empty;
    public Guid? TargetEntityId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
}
