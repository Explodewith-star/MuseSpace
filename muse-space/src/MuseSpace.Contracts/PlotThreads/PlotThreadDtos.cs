namespace MuseSpace.Contracts.PlotThreads;

public sealed class PlotThreadResponse
{
    public Guid Id { get; set; }
    public Guid StoryProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Importance { get; set; }
    public string Status { get; set; } = "Introduced";
    public Guid? PlantedInChapterId { get; set; }
    public Guid? ResolvedInChapterId { get; set; }
    public List<Guid>? RelatedCharacterIds { get; set; }
    /// <summary>预期回收于第几章。</summary>
    public int? ExpectedResolveByChapterNumber { get; set; }
    public string? Tags { get; set; }
    // ── 作用域字段 ──
    public Guid? OutlineId { get; set; }
    public Guid? ChainId { get; set; }
    /// <summary>ThisOutline / Chain / Project</summary>
    public string Visibility { get; set; } = "Chain";
    public Guid? ResolvedInOutlineId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class UpsertPlotThreadRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Importance { get; set; }
    /// <summary>Introduced / Active / PaidOff / Abandoned。</summary>
    public string? Status { get; set; }
    public Guid? PlantedInChapterId { get; set; }
    public Guid? ResolvedInChapterId { get; set; }
    public List<Guid>? RelatedCharacterIds { get; set; }
    /// <summary>预期回收于第几章。</summary>
    public int? ExpectedResolveByChapterNumber { get; set; }
    public string? Tags { get; set; }
    /// <summary>
    /// 可见性作用域：ThisOutline / Chain / Project。
    /// 不传则保持原值（新建时默认 Chain）。
    /// </summary>
    public string? Visibility { get; set; }
}

