namespace MuseSpace.Domain.Entities;

public class StoryProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Genre { get; set; }
    public string? NarrativePerspective { get; set; }
    public Guid? DefaultStyleProfileId { get; set; }
    /// <summary>null = 游客共享项目；有值 = 该用户的私有项目</summary>
    public Guid? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
