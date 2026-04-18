namespace MuseSpace.Contracts.WorldRules;

public sealed class UpdateWorldRuleRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public int? Priority { get; init; }
    public bool? IsHardConstraint { get; init; }
}
