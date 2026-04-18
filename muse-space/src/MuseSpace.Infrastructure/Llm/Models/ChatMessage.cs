using System.Text.Json.Serialization;

namespace MuseSpace.Infrastructure.Llm.Models;

public sealed class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}
