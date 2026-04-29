using MuseSpace.Application.Abstractions.Llm;

namespace MuseSpace.Infrastructure.Llm;

/// <summary>
/// 运行时路由 LLM 客户端：根据 <see cref="LlmProviderSelector.Active"/> 委托到
/// OpenRouter、DeepSeek 或 Venice 实现。
/// </summary>
public sealed class RoutingLlmClient : ILlmClient
{
    private readonly OpenRouterLlmClient _openRouter;
    private readonly DeepSeekLlmClient _deepSeek;
    private readonly VeniceLlmClient _venice;
    private readonly LlmProviderSelector _selector;

    public RoutingLlmClient(
        OpenRouterLlmClient openRouter,
        DeepSeekLlmClient deepSeek,
        VeniceLlmClient venice,
        LlmProviderSelector selector)
    {
        _openRouter = openRouter;
        _deepSeek = deepSeek;
        _venice = venice;
        _selector = selector;
    }

    public Task<string> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
        => _selector.Active switch
        {
            LlmProviderType.DeepSeek => _deepSeek.ChatAsync(systemPrompt, userPrompt, cancellationToken),
            LlmProviderType.Venice   => _venice.ChatAsync(systemPrompt, userPrompt, cancellationToken),
            _                        => _openRouter.ChatAsync(systemPrompt, userPrompt, cancellationToken),
        };
}

