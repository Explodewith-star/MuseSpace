namespace MuseSpace.Application.Abstractions.Llm;

public enum LlmProviderType
{
    OpenRouter,
    DeepSeek,
}

/// <summary>
/// 运行时可切换的 LLM 渠道选择器（单例，线程安全）。
/// ActiveModel 为 null 时表示使用配置文件中的默认模型。
/// </summary>
public sealed class LlmProviderSelector
{
    private volatile LlmProviderType _active = LlmProviderType.OpenRouter;
    private volatile string? _activeModel = null;

    public LlmProviderType Active
    {
        get => _active;
        set => _active = value;
    }

    /// <summary>当前 OpenRouter 使用的模型，null 表示用配置文件默认值。</summary>
    public string? ActiveModel
    {
        get => _activeModel;
        set => _activeModel = value;
    }
}
