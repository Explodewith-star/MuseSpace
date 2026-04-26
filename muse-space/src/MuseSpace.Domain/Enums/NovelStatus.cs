namespace MuseSpace.Domain.Enums;

public enum NovelStatus
{
    /// <summary>已创建记录，等待处理</summary>
    Pending = 0,

    /// <summary>正在切片</summary>
    Chunking = 1,

    /// <summary>已完成向量化，可供检索</summary>
    Indexed = 2,

    /// <summary>处理失败</summary>
    Failed = 3,

    /// <summary>正在向量化</summary>
    Embedding = 4
}
