namespace MuseSpace.Application.Abstractions.Memory;

/// <summary>
/// 向量检索的单条结果。
/// </summary>
public sealed class NovelChunkSearchResult
{
    public Guid ChunkId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }

    /// <summary>余弦相似度，范围 [0, 1]，越高越相关</summary>
    public double Similarity { get; set; }
}
