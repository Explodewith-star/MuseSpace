namespace MuseSpace.Contracts.WorldRules;

public sealed class CreateWorldRuleRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public int Priority { get; init; } = 5;
    public bool IsHardConstraint { get; init; }
}
