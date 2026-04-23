namespace MuseSpace.Application.Abstractions.Llm;

/// <summary>
/// LLM 服务的配置选项，由 appsettings.json 中的 "Llm" 节点绑定。
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>API 基础地址，例如 https://openrouter.ai/api/v1</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>API 密钥（应放在本地配置中，不提交到仓库）</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>默认模型名称，例如 openai/gpt-oss-120b:free</summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>可选模型列表，供前端下拉框展示。为空时前端只显示默认模型。</summary>
    public List<ModelOption> AvailableModels { get; init; } = [];
}

public sealed class ModelOption
{
    /// <summary>模型标识，对应 OpenRouter model 字段，例如 openai/gpt-oss-120b:free</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>展示名称，例如 GPT-4o mini (免费)</summary>
    public string Label { get; init; } = string.Empty;
}
