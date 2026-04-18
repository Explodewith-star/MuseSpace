namespace MuseSpace.Contracts.Llm;

public sealed class ChatRequest
{
    /// <summary>用户提问</summary>
    public string Question { get; init; } = string.Empty;

    /// <summary>可选的系统角色设定，留空时使用默认值</summary>
    public string? SystemPrompt { get; init; }
}
