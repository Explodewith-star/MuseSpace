using System.Text;
using System.Text.Json;
using MuseSpace.Infrastructure.Llm.Models;

namespace MuseSpace.Infrastructure.Llm;

/// <summary>
/// SSE 流读取结果，包含拼接内容和 Token 用量。
/// </summary>
internal sealed class SseReadResult
{
    public string Content { get; init; } = string.Empty;
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
}

/// <summary>
/// 读取 OpenAI 兼容 SSE 流，将所有 delta.content 拼接为完整字符串，
/// 并从最后一个 chunk 中提取 usage 信息。
/// </summary>
internal static class SseStreamReader
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<SseReadResult> ReadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        ChatCompletionUsage? usage = null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;

            // SSE 格式：每行以 "data: " 开头
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var json = line["data:".Length..].Trim();
            if (json == "[DONE]") break;
            if (string.IsNullOrEmpty(json)) continue;

            ChatCompletionStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<ChatCompletionStreamChunk>(json, _jsonOptions);
            }
            catch (JsonException)
            {
                // 忽略无法解析的行
                continue;
            }

            var content = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(content))
                sb.Append(content);

            // 捕获最后一个 chunk 中的 usage 信息
            if (chunk?.Usage is not null)
                usage = chunk.Usage;
        }

        return new SseReadResult
        {
            Content = sb.ToString(),
            PromptTokens = usage?.PromptTokens ?? 0,
            CompletionTokens = usage?.CompletionTokens ?? 0
        };
    }
}
