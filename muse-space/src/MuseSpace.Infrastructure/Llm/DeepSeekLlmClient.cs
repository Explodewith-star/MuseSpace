using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Infrastructure.Llm.Models;

namespace MuseSpace.Infrastructure.Llm;

/// <summary>
/// 通过 DeepSeek 官方 API 调用 LLM（OpenAI 兼容接口）。
/// </summary>
public sealed class DeepSeekLlmClient
{
    private const int MaxAttempts = 3;

    private readonly HttpClient _httpClient;
    private readonly DeepSeekOptions _options;
    private readonly ILogger<DeepSeekLlmClient> _logger;

    public DeepSeekLlmClient(
        HttpClient httpClient,
        IOptions<DeepSeekOptions> options,
        ILogger<DeepSeekLlmClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LlmChatResult> ChatAsync(
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

        _logger.LogInformation("Calling DeepSeek model {Model}...", _options.ModelName);

        HttpResponseMessage response;
        try
        {
            response = await SendWithRetryAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP request to DeepSeek failed");
            throw new InvalidOperationException("Failed to call DeepSeek API.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("DeepSeek returned {StatusCode}: {Body}",
                (int)response.StatusCode, errorBody);
            throw new InvalidOperationException(
                $"DeepSeek API returned HTTP {(int)response.StatusCode}: {errorBody}");
        }

        var result = await SseStreamReader.ReadAsync(response, cancellationToken);

        if (string.IsNullOrWhiteSpace(result.Content))
        {
            _logger.LogWarning("DeepSeek returned empty content");
            return new LlmChatResult();
        }

        _logger.LogInformation("DeepSeek returned {Length} chars, tokens in={In} out={Out}",
            result.Content.Length, result.PromptTokens, result.CompletionTokens);
        return new LlmChatResult
        {
            Content = result.Content,
            InputTokens = result.PromptTokens,
            OutputTokens = result.CompletionTokens
        };
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
                {
                    Content = JsonContent.Create(request)
                };

                return await _httpClient.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
            }
            catch (Exception ex) when (IsTransient(ex, cancellationToken) && attempt < MaxAttempts)
            {
                lastException = ex;
                _logger.LogWarning(ex,
                    "DeepSeek request failed on attempt {Attempt}/{MaxAttempts}, retrying...",
                    attempt,
                    MaxAttempts);
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        throw lastException ?? new InvalidOperationException("DeepSeek request failed unexpectedly.");
    }

    private static bool IsTransient(Exception ex, CancellationToken cancellationToken)
    {
        if (ex is HttpRequestException)
        {
            return true;
        }

        if (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return true;
        }

        return false;
    }
}
