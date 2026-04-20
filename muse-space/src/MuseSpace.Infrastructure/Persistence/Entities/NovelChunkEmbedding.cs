using Pgvector;

namespace MuseSpace.Infrastructure.Persistence.Entities;

/// <summary>
/// 原著切片的向量表示，存放于 memory schema。
/// 使用硅基流动 BAAI/bge-m3 模型，维度为 1024。
/// 此实体仅用于持久化，不暴露到 Domain 层。
/// </summary>
public class NovelChunkEmbedding
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>关联的 novel_chunks.id</summary>
    public Guid ChunkId { get; set; }

    /// <summary>冗余存储，方便按项目范围检索</summary>
    public Guid StoryProjectId { get; set; }

    /// <summary>生成此向量使用的模型名，如 BAAI/bge-m3</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>1024 维向量，使用 HNSW 余弦索引</summary>
    public Vector Embedding { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
