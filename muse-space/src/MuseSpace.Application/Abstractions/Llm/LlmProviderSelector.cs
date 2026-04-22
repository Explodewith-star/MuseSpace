namespace MuseSpace.Application.Abstractions.Llm;

public enum LlmProviderType
{
    OpenRouter,
    DeepSeek,
}

/// <summary>
/// 运行时可切换的 LLM 渠道选择器（单例，线程安全）。
/// </summary>
public sealed class LlmProviderSelector
{
    private volatile LlmProviderType _active = LlmProviderType.OpenRouter;

    public LlmProviderType Active
    {
        get => _active;
        set => _active = value;
    }
}
