using MuseSpace.Application.Abstractions.Llm;

namespace MuseSpace.Infrastructure.Llm;

/// <summary>
/// 运行时路由 LLM 客户端：根据 <see cref="LlmProviderSelector.Active"/> 委托到
/// OpenRouter 或 DeepSeek 实现。
/// </summary>
public sealed class RoutingLlmClient : ILlmClient
{
    private readonly OpenRouterLlmClient _openRouter;
    private readonly DeepSeekLlmClient _deepSeek;
    private readonly LlmProviderSelector _selector;

    public RoutingLlmClient(
        OpenRouterLlmClient openRouter,
        DeepSeekLlmClient deepSeek,
        LlmProviderSelector selector)
    {
        _openRouter = openRouter;
        _deepSeek = deepSeek;
        _selector = selector;
    }

    public Task<string> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
        => _selector.Active switch
        {
            LlmProviderType.DeepSeek => _deepSeek.ChatAsync(systemPrompt, userPrompt, cancellationToken),
            _ => _openRouter.ChatAsync(systemPrompt, userPrompt, cancellationToken),
        };
}
