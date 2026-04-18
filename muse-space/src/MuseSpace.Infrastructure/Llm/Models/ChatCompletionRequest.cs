using System.Text.Json.Serialization;

namespace MuseSpace.Infrastructure.Llm.Models;

/// <summary>
/// OpenRouter /chat/completions 请求体。
/// </summary>
public sealed class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; init; } = [];
}
