namespace MuseSpace.Application.Abstractions.Llm;

/// <summary>
/// Embedding（向量化）客户端接口，与 ILlmClient 平级，独立实现。
/// </summary>
public interface IEmbeddingClient
{
    /// <summary>模型名称，如 BAAI/bge-m3</summary>
    string ModelName { get; }

    /// <summary>向量维度，如 1024</summary>
    int Dimensions { get; }

    /// <summary>将文本转换为向量</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);

    /// <summary>批量将文本转换为向量，返回结果顺序与输入顺序一致</summary>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
