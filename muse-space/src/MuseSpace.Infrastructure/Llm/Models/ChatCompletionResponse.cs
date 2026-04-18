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
