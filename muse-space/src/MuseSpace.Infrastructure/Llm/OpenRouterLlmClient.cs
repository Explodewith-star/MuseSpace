using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Infrastructure.Llm.Models;

namespace MuseSpace.Infrastructure.Llm;

/// <summary>
/// 通过 OpenRouter API 调用 LLM 的真实实现。
/// 替代 Phase 1 的 LocalModelClient stub。
/// </summary>
public sealed class OpenRouterLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<OpenRouterLlmClient> _logger;

    public OpenRouterLlmClient(
        HttpClient httpClient,
        IOptions<LlmOptions> options,
        ILogger<OpenRouterLlmClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var request = new ChatCompletionRequest
        {
            Model = _options.ModelName,
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userPrompt }
            ]
        };

        _logger.LogInformation("Calling OpenRouter model {Model}...", _options.ModelName);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                "chat/completions",
                request,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP request to OpenRouter failed");
            throw new InvalidOperationException("Failed to call OpenRouter API.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("OpenRouter returned {StatusCode}: {Body}",
                (int)response.StatusCode, errorBody);
            throw new InvalidOperationException(
                $"OpenRouter API returned HTTP {(int)response.StatusCode}: {errorBody}");
        }

        var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(
            cancellationToken: cancellationToken);

        var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("OpenRouter returned empty content");
            return string.Empty;
        }

        _logger.LogInformation("OpenRouter returned {Length} chars", content.Length);
        return content;
    }
}
