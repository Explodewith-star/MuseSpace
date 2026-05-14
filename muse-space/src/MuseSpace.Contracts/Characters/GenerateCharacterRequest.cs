namespace MuseSpace.Contracts.Characters;

public sealed class GenerateCharacterRequest
{
    /// <summary>用户对角色的文字描述，如"一个冷酷的剑客，30岁，曾是皇城禁卫"。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>是否从原著中提取（true 时使用向量检索 + 提取 Agent）。</summary>
    public bool FromNovel { get; init; }
}
