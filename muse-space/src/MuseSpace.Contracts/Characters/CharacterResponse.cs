namespace MuseSpace.Contracts.Characters;

public sealed class CharacterResponse
{
    public Guid Id { get; init; }
    public Guid StoryProjectId { get; init; }
    /// <summary>来源原著 ID，null = 用户原创角色，有值 = 从该原著提取。</summary>
    public Guid? SourceNovelId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? Age { get; init; }
    public string? Role { get; init; }
    public string? PersonalitySummary { get; init; }
    public string? Motivation { get; init; }
    public string? SpeakingStyle { get; init; }
    public string? ForbiddenBehaviors { get; init; }
    public string? CurrentState { get; init; }
    public string? Tags { get; init; }
    public string? Category { get; init; }
}
