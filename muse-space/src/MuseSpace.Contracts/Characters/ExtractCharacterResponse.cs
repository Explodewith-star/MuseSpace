namespace MuseSpace.Contracts.Characters;

/// <summary>
/// AI 从原著提取的角色信息（仅建议值，未保存到数据库）
/// </summary>
public sealed class ExtractCharacterResponse
{
    public string Name { get; init; } = string.Empty;
    public int? Age { get; init; }
    public string? Role { get; init; }
    public string? PersonalitySummary { get; init; }
    public string? Motivation { get; init; }
    public string? SpeakingStyle { get; init; }
    public string? ForbiddenBehaviors { get; init; }
    public string? CurrentState { get; init; }
    public string? Category { get; init; }
    /// <summary>AI 使用了几个原著片段作为依据</summary>
    public int SourceChunkCount { get; init; }
}
