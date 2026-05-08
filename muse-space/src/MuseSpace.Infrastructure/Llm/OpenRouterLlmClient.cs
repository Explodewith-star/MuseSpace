using System.Net.Http.Json;
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
    private readonly LlmProviderSelector _selector;
    private readonly ILogger<OpenRouterLlmClient> _logger;

    public OpenRouterLlmClient(
        HttpClient httpClient,
        IOptions<LlmOptions> options,
        LlmProviderSelector selector,
        ILogger<OpenRouterLlmClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _selector = selector;
        _logger = logger;
    }

    public async Task<LlmChatResult> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        // 运行时切换的模型优先，否则用配置文件默认值
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

        _logger.LogInformation("Calling OpenRouter model {Model}...", modelName);

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

        var result = await SseStreamReader.ReadAsync(response, cancellationToken);

        if (string.IsNullOrWhiteSpace(result.Content))
        {
            _logger.LogWarning("OpenRouter returned empty content");
            return new LlmChatResult();
        }

        _logger.LogInformation("OpenRouter returned {Length} chars, tokens in={In} out={Out}",
            result.Content.Length, result.PromptTokens, result.CompletionTokens);
        return new LlmChatResult
        {
            Content = result.Content,
            InputTokens = result.PromptTokens,
            OutputTokens = result.CompletionTokens
        };
    }
}
