namespace MuseSpace.Contracts.Characters;

public sealed class CharacterResponse
{
    public Guid Id { get; init; }
    public Guid StoryProjectId { get; init; }
    /// <summary>
    /// 归属的大纲 ID。
    /// null = 原著角色池（项目级只读参考）。
    /// 有值 = 已引入某个具体大纲。
    /// </summary>
    public Guid? StoryOutlineId { get; init; }
    /// <summary>来源原著 ID，null = 用户原创角色，有值 = 从该原著提取。</summary>
    public Guid? SourceNovelId { get; init; }
    /// <summary>来源角色池原始 ID，null = 直接在大纲/池中新建。</summary>
    public Guid? SourcePoolCharacterId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? Age { get; init; }
    public string? Role { get; init; }
    public string? PersonalitySummary { get; init; }
    public string? Motivation { get; init; }
    public string? SpeakingStyle { get; init; }
    public string? ForbiddenBehaviors { get; init; }
    public string? CurrentState { get; init; }
    public string? Tags { get; init; }
}
