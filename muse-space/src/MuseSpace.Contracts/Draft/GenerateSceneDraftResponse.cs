namespace MuseSpace.Contracts.Draft;

public sealed class GenerateSceneDraftResponse
{
    public string RequestId { get; init; } = string.Empty;
    public string GeneratedText { get; init; } = string.Empty;
    public string? SkillName { get; init; }
    public string? PromptVersion { get; init; }
    public long DurationMs { get; init; }
}
