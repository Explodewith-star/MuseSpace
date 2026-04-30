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
    public string? Tags { get; set; }
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
    public string? Tags { get; set; }
}
