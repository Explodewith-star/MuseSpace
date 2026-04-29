namespace MuseSpace.Contracts.Suggestions;

/// <summary>更新建议正文 JSON 的请求体（允许对任意状态的建议进行大纲内容编辑）。</summary>
public sealed class UpdateSuggestionContentRequest
{
    /// <summary>新的 ContentJson，必须是合法 JSON 字符串。</summary>
    public string ContentJson { get; set; } = "{}";
}
