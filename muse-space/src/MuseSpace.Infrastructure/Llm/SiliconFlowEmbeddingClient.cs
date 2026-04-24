using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MuseSpace.Application.Abstractions.Llm;

namespace MuseSpace.Infrastructure.Llm;

/// <summary>
/// 通过硅基流动（SiliconFlow）API 调用 BAAI/bge-m3 生成向量。
/// API 兼容 OpenAI embeddings 接口格式。
/// </summary>
public sealed class SiliconFlowEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingOptions _options;
    private readonly ILogger<SiliconFlowEmbeddingClient> _logger;

    public string ModelName => _options.ModelName;
    public int Dimensions => _options.Dimensions;

    public SiliconFlowEmbeddingClient(
        HttpClient httpClient,
        IOptions<EmbeddingOptions> options,
        ILogger<SiliconFlowEmbeddingClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var request = new EmbeddingRequest { Model = _options.ModelName, Input = text };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("embeddings", request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SiliconFlow embedding HTTP request failed");
            throw new InvalidOperationException("Failed to call SiliconFlow embedding API.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("SiliconFlow embedding returned {Status}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException(
                $"SiliconFlow embedding API returned HTTP {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct);
        var embedding = result?.Data?.FirstOrDefault()?.Embedding;

        if (embedding is null || embedding.Length == 0)
            throw new InvalidOperationException("SiliconFlow returned empty embedding data.");

        return embedding;
    }

    private sealed class EmbeddingRequest
    {
        [JsonPropertyName("model")] public string Model { get; init; } = string.Empty;
        [JsonPropertyName("input")] public string Input { get; init; } = string.Empty;
    }

    private sealed class EmbeddingResponse
    {
        [JsonPropertyName("data")] public List<EmbeddingData>? Data { get; init; }
    }

    private sealed class EmbeddingData
    {
        [JsonPropertyName("embedding")] public float[]? Embedding { get; init; }
    }
}
