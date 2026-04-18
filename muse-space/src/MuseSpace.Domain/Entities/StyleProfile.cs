namespace MuseSpace.Domain.Entities;

public class StyleProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SentenceLengthPreference { get; set; }
    public string? DialogueRatio { get; set; }
    public string? DescriptionDensity { get; set; }
    public string? Tone { get; set; }
    public string? ForbiddenExpressions { get; set; }
    public string? SampleReferenceText { get; set; }
}
