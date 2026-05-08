namespace MuseSpace.Application.Abstractions.Llm;

/// <summary>
/// LLM 调用结果，包含生成内容和 Token 用量。
/// </summary>
public sealed class LlmChatResult
{
    public string Content { get; init; } = string.Empty;
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int TotalTokens => InputTokens + OutputTokens;
}
