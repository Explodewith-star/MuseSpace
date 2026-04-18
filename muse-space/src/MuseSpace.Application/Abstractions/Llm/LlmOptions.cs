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

    /// <summary>模型名称，例如 openai/gpt-oss-120b:free</summary>
    public string ModelName { get; init; } = string.Empty;
}
