using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Infrastructure.Llm.Models;

namespace MuseSpace.Infrastructure.Llm;

/// <summary>
/// 通过 Venice AI API 调用 LLM（OpenAI 兼容接口）。
/// 支持多模型切换，当前选中模型从 <see cref="LlmProviderSelector"/> 读取。
/// 仅限管理员使用。
/// </summary>
public sealed class VeniceLlmClient
{
    private readonly HttpClient _httpClient;
    private readonly VeniceOptions _options;
    private readonly LlmProviderSelector _selector;
    private readonly ILogger<VeniceLlmClient> _logger;

    public VeniceLlmClient(
        HttpClient httpClient,
        IOptions<VeniceOptions> options,
        LlmProviderSelector selector,
        ILogger<VeniceLlmClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _selector = selector;
        _logger = logger;
    }

    public async Task<string> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var modelName = _selector.ActiveModel ?? _options.ModelName;
        var request = new ChatCompletionRequest
        {
            Model = modelName,
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userPrompt }
            ]
        };

        _logger.LogInformation("Calling Venice model {Model}...", modelName);

        HttpResponseMessage response;
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = JsonContent.Create(request)
            };
            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP request to Venice failed");
            throw new InvalidOperationException("Failed to call Venice API.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Venice returned {StatusCode}: {Body}",
                (int)response.StatusCode, errorBody);
            throw new InvalidOperationException(
                $"Venice API returned HTTP {(int)response.StatusCode}: {errorBody}");
        }

        var content = await SseStreamReader.ReadAsync(response, cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Venice returned empty content");
            return string.Empty;
        }

        _logger.LogInformation("Venice returned {Length} chars", content.Length);
        return content;
    }
}
