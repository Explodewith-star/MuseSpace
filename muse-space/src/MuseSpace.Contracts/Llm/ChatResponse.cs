namespace MuseSpace.Contracts.Llm;

public sealed class ChatResponse
{
    /// <summary>模型的回答</summary>
    public string Answer { get; init; } = string.Empty;

    /// <summary>本次请求的耗时（毫秒）</summary>
    public long DurationMs { get; init; }
}
