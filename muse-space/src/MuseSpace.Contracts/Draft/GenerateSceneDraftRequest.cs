namespace MuseSpace.Contracts.Draft;

public sealed class GenerateSceneDraftRequest
{
    public Guid StoryProjectId { get; init; }
    public string SceneGoal { get; init; } = string.Empty;
    public string? Conflict { get; init; }
    public string? EmotionCurve { get; init; }
    public string? ReferenceText { get; init; }
    public string? ReferenceFocus { get; init; }
    public string? ReferenceStrength { get; init; }
}
