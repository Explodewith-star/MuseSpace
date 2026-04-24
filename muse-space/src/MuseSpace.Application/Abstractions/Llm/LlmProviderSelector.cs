namespace MuseSpace.Application.Abstractions.Llm;

public enum LlmProviderType
{
    OpenRouter,
    DeepSeek,
}

/// <summary>
/// 运行时 LLM 渠道选择器。
/// ⚠️ 注册为 Scoped：每个 HTTP 请求内一致，跨请求/跨用户隔离。
/// 由 <c>LlmPreferenceInitializer</c> 在请求开始时根据当前登录用户的偏好填充。
/// 游客请求（无 JWT）保持默认值 OpenRouter，<c>ActiveModel</c> 为 null（走配置默认）。
/// </summary>
public sealed class LlmProviderSelector
{
    private LlmProviderType _active = LlmProviderType.OpenRouter;
    private string? _activeModel;

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
