namespace MuseSpace.Domain.Entities;

public class WorldRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryProjectId { get; set; }

    /// <summary>从哪部原著提取（null = 用户原创规则）</summary>
    public Guid? SourceNovelId { get; set; }

    public string? Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Priority { get; set; }
    public bool IsHardConstraint { get; set; }
}
