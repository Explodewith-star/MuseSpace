using MuseSpace.Domain.Enums;

namespace MuseSpace.Domain.Entities;

/// <summary>导入的原著小说文件记录</summary>
public class Novel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryProjectId { get; set; }

    /// <summary>小说标题（显示用）</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>上传时的原始文件名</summary>
    public string? FileName { get; set; }

    /// <summary>逻辑存储路径，如 raw/novels/xxx.txt</summary>
    public string? FileKey { get; set; }

    /// <summary>SHA-256 文件哈希，用于去重</summary>
    public string? FileHash { get; set; }

    public long? FileSize { get; set; }

    /// <summary>处理状态：Pending → Processing → Indexed / Failed</summary>
    public NovelStatus Status { get; set; } = NovelStatus.Pending;

    /// <summary>已生成的切片总数</summary>
    public int TotalChunks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
