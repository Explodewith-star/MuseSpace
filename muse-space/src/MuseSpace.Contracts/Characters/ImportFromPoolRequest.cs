namespace MuseSpace.Contracts.Characters;

/// <summary>
/// 从原著角色池引入角色到大纲的请求。
/// </summary>
public sealed class ImportFromPoolRequest
{
    /// <summary>要引入的角色 ID 列表（必须来自原著角色池）。</summary>
    public List<Guid> CharacterIds { get; init; } = [];
}
