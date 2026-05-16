namespace MuseSpace.Contracts.Characters;

/// <summary>
/// 将指定角色复制到目标大纲（隔离复制，各自独立演化）。
/// </summary>
public sealed class CopyCharactersRequest
{
    /// <summary>要复制的角色 ID 列表。</summary>
    public List<Guid> CharacterIds { get; init; } = [];

    /// <summary>目标大纲 ID。</summary>
    public Guid TargetOutlineId { get; init; }
}
