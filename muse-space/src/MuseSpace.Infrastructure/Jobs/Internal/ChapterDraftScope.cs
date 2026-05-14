using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

namespace MuseSpace.Infrastructure.Jobs.Internal;

public sealed class FutureChapterSignature
{
    public int ChapterNumber { get; init; }
    public string Title { get; init; } = string.Empty;
    public List<string> Signals { get; init; } = [];
}

public sealed class ChapterDraftScope
{
    public Guid ProjectId { get; init; }
    public Guid ChapterId { get; init; }
    public Guid OutlineId { get; init; }
    public int ChapterNumber { get; init; }
    public string CurrentPlanText { get; init; } = string.Empty;
    public List<string> AllowedCharacters { get; init; } = [];
    public List<string> AllowedLocations { get; init; } = [];
    public List<string> RequiredBeats { get; init; } = [];
    public ChapterRevealLevel AllowedRevealLevel { get; init; }
    public GenerationMode GenerationMode { get; init; } = GenerationMode.Original;
    public DivergencePolicy DivergencePolicy { get; init; } = DivergencePolicy.SoftCanon;
    public Guid? SourceNovelId { get; init; }
    public int? SourceRangeStart { get; init; }
    public int? SourceRangeEnd { get; init; }
    public string? BranchTopic { get; init; }
    public string? ContinuationAnchor { get; init; }
    public List<string> ReservedFutureBeats { get; init; } = [];
    public List<Chapter> FutureChapters { get; init; } = [];
    public List<FutureChapterSignature> FutureChapterSignatures { get; init; } = [];
    public string BoundaryInstruction { get; init; } = string.Empty;

    public string ToLogSummary()
        => $"outline={OutlineId}, chapter={ChapterNumber}, mode={GenerationMode}, reveal={AllowedRevealLevel}, " +
           $"sourceNovel={(SourceNovelId.HasValue ? SourceNovelId.Value : "none")}, " +
           $"requiredBeats={RequiredBeats.Count}, futureBeats={ReservedFutureBeats.Count}";
}
