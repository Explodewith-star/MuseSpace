using System.Text.Json.Serialization;

namespace MuseSpace.Infrastructure.Llm.Models;

/// <summary>
/// OpenRouter /chat/completions 响应体（只映射需要的字段）。
/// </summary>
public sealed class ChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<ChatChoice>? Choices { get; init; }
}

public sealed class ChatChoice
{
    [JsonPropertyName("message")]
    public ChatMessage? Message { get; init; }
}

/// <summary>
/// SSE 流式响应的单个 chunk。
/// </summary>
public sealed class ChatCompletionStreamChunk
{
    [JsonPropertyName("choices")]
    public List<ChatStreamChoice>? Choices { get; init; }

    [JsonPropertyName("usage")]
    public ChatCompletionUsage? Usage { get; init; }
}

public sealed class ChatStreamChoice
{
    [JsonPropertyName("delta")]
    public ChatDelta? Delta { get; init; }
}

public sealed class ChatDelta
{
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}

/// <summary>
/// Token 用量统计（OpenAI 兼容格式）。
/// </summary>
public sealed class ChatCompletionUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}
