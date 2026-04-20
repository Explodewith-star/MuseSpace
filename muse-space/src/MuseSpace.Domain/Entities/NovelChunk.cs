namespace MuseSpace.Domain.Entities;

/// <summary>原著小说的文本切片，每段约 800 字符</summary>
public class NovelChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NovelId { get; set; }

    /// <summary>冗余存储，方便按项目过滤</summary>
    public Guid StoryProjectId { get; set; }

    /// <summary>切片序号，从 0 开始</summary>
    public int ChunkIndex { get; set; }

    public string Content { get; set; } = string.Empty;

    public int CharCount { get; set; }
    public int? TokenCount { get; set; }

    /// <summary>在原文中的起始字符偏移量</summary>
    public int StartOffset { get; set; }

    /// <summary>在原文中的结束字符偏移量</summary>
    public int EndOffset { get; set; }

    /// <summary>是否已生成向量 Embedding</summary>
    public bool IsEmbedded { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
