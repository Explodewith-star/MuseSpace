namespace MuseSpace.Application.Abstractions.Story;

public class StoryContextRequest
{
    public Guid StoryProjectId { get; init; }
    public Guid? ChapterId { get; init; }
    public Guid? OutlineId { get; init; }
    public Guid? SceneId { get; init; }
    public List<Guid>? InvolvedCharacterIds { get; init; }
    public Guid? StyleProfileId { get; init; }
    public string SceneGoal { get; init; } = string.Empty;
    public string? Conflict { get; init; }
    public string? EmotionCurve { get; init; }
    public bool IncludeNovelContext { get; init; }

    // ── Module E：续写/外传模式 ─────────────────────────────────────
    public Domain.Enums.GenerationMode GenerationMode { get; init; } = Domain.Enums.GenerationMode.Original;
    public Guid? SourceNovelId { get; init; }
    public int? ContinuationStartChapterNumber { get; init; }
    public int? OriginalRangeStart { get; init; }
    public int? OriginalRangeEnd { get; init; }
    public List<Guid>? RelatedCharacterIds { get; init; }
    public string? BranchTopic { get; init; }
    public Domain.Enums.DivergencePolicy DivergencePolicy { get; init; } = Domain.Enums.DivergencePolicy.SoftCanon;
}
