namespace MuseSpace.Contracts.StyleProfiles;

public sealed class UpsertStyleProfileRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Tone { get; init; }
    public string? SentenceLengthPreference { get; init; }
    public string? DialogueRatio { get; init; }
    public string? DescriptionDensity { get; init; }
    public string? ForbiddenExpressions { get; init; }
    public string? SampleReferenceText { get; init; }
}
