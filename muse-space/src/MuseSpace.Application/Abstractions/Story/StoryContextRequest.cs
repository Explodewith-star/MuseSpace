namespace MuseSpace.Application.Abstractions.Story;

public class StoryContextRequest
{
    public Guid StoryProjectId { get; init; }
    public Guid? ChapterId { get; init; }
    public Guid? SceneId { get; init; }
    public List<Guid>? InvolvedCharacterIds { get; init; }
    public Guid? StyleProfileId { get; init; }
    public string SceneGoal { get; init; } = string.Empty;
    public string? Conflict { get; init; }
    public string? EmotionCurve { get; init; }
}
