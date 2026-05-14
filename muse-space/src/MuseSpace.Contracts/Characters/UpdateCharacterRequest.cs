namespace MuseSpace.Contracts.Characters;

public sealed class UpdateCharacterRequest
{
    public string? Name { get; init; }
    public int? Age { get; init; }
    public string? Role { get; init; }
    public string? PersonalitySummary { get; init; }
    public string? Motivation { get; init; }
    public string? SpeakingStyle { get; init; }
    public string? ForbiddenBehaviors { get; init; }
    public string? PublicSecrets { get; init; }
    public string? PrivateSecrets { get; init; }
    public string? CurrentState { get; init; }
    public string? Tags { get; init; }
    public string? Category { get; init; }
}
