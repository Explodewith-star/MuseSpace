using MuseSpace.Domain.Enums;

namespace MuseSpace.Domain.Entities;

public class StoryOutline
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public GenerationMode Mode { get; set; } = GenerationMode.Original;
    public Guid? SourceNovelId { get; set; }
    public int? SourceRangeStart { get; set; }
    public int? SourceRangeEnd { get; set; }
    public string? BranchTopic { get; set; }
    public string? ContinuationAnchor { get; set; }
    public DivergencePolicy DivergencePolicy { get; set; } = DivergencePolicy.SoftCanon;
    public int? TargetChapterCount { get; set; }
    public string? OutlineSummary { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
