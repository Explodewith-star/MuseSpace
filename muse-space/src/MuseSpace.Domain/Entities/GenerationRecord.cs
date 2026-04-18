namespace MuseSpace.Domain.Entities;

public class GenerationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequestId { get; set; } = string.Empty;
    public Guid? StoryProjectId { get; set; }
    public string? TaskType { get; set; }
    public string? SkillName { get; set; }
    public string? PromptName { get; set; }
    public string? PromptVersion { get; set; }
    public string? ModelName { get; set; }
    public long DurationMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? InputPreview { get; set; }
    public string? OutputPreview { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
