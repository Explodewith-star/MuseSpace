namespace MuseSpace.Application.Abstractions.Llm;

/// <summary>
/// Embedding 服务配置，绑定 appsettings.json 的 "Embedding" 节。
/// </summary>
public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    /// <summary>API 基础地址，如 https://api.siliconflow.cn/v1</summary>
    public string BaseUrl { get; init; } = "https://api.siliconflow.cn/v1";

    /// <summary>Embedding 模型名，如 BAAI/bge-m3</summary>
    public string ModelName { get; init; } = "BAAI/bge-m3";

    /// <summary>向量维度，需与数据库 vector(N) 定义一致</summary>
    public int Dimensions { get; init; } = 1024;

    /// <summary>API 密钥，通过 dotnet user-secrets 管理，勿提交至 Git</summary>
    public string ApiKey { get; init; } = string.Empty;
}
