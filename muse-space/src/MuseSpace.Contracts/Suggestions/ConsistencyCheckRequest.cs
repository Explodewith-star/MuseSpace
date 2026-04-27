namespace MuseSpace.Contracts.Suggestions;

public sealed class ConsistencyCheckRequest
{
    /// <summary>要检查的草稿文本。</summary>
    public string DraftText { get; init; } = string.Empty;
}
