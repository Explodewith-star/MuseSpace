namespace MuseSpace.Application.Abstractions.Skills;

public class SkillResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public string SkillName { get; init; } = string.Empty;
    public string? PromptVersion { get; init; }
    public long DurationMs { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
}
