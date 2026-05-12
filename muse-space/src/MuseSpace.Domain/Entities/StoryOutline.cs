using MuseSpace.Domain.Enums;

namespace MuseSpace.Domain.Entities;

public class StoryOutline
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public GenerationMode Mode { get; set; } = GenerationMode.Original;

    /// <summary>所属故事链（null = 未归链，兼容旧数据）</summary>
    public Guid? ChainId { get; set; }

    /// <summary>链内排序序号（1,2,3...）</summary>
    public int ChainIndex { get; set; }

    /// <summary>直接前驱批次 ID（续写时用于上下文衔接）</summary>
    public Guid? PreviousOutlineId { get; set; }

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
