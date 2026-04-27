namespace MuseSpace.Contracts.Suggestions;

public sealed class BatchResolveSuggestionsRequest
{
    /// <summary>要操作的建议 ID 列表。</summary>
    public List<Guid> Ids { get; init; } = [];

    /// <summary>操作类型：Accept 或 Ignore。</summary>
    public string Action { get; init; } = string.Empty;
}
