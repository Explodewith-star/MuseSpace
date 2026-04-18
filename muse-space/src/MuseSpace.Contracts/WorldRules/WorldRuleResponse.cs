namespace MuseSpace.Contracts.WorldRules;

public sealed class WorldRuleResponse
{
    public Guid Id { get; init; }
    public Guid StoryProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public int Priority { get; init; }
    public bool IsHardConstraint { get; init; }
}
