namespace MuseSpace.Domain.Entities;

public class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Age { get; set; }
    public string? Role { get; set; }
    public string? PersonalitySummary { get; set; }
    public string? Motivation { get; set; }
    public string? SpeakingStyle { get; set; }
    public string? ForbiddenBehaviors { get; set; }
    public string? PublicSecrets { get; set; }
    public string? PrivateSecrets { get; set; }
    public string? CurrentState { get; set; }
    public string? Tags { get; set; }
}
