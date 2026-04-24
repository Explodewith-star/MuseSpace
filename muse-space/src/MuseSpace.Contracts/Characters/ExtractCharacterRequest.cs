namespace MuseSpace.Contracts.Characters;

/// <summary>
/// 请求从原著向量库中提取角色信息
/// </summary>
public sealed class ExtractCharacterRequest
{
    /// <summary>描述要提取的角色，如"主角"、"石泓"、"女主角"</summary>
    public string Query { get; init; } = string.Empty;
}
