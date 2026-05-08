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

    /// <summary>处理状态：Pending → Chunking → Embedding → Indexed / Failed</summary>
    public NovelStatus Status { get; set; } = NovelStatus.Pending;

    /// <summary>已生成的切片总数</summary>
    public int TotalChunks { get; set; }

    /// <summary>当前阶段已处理进度</summary>
    public int ProgressDone { get; set; }

    /// <summary>当前阶段总量</summary>
    public int ProgressTotal { get; set; }

    /// <summary>最近一次失败原因</summary>
    public string? LastError { get; set; }

    /// <summary>导入处理开始时间</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>导入处理结束时间</summary>
    public DateTime? FinishedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Module E+：续写/番外质量增强 ──────────────────────────────────

    /// <summary>
    /// 原著大结局摘要（由 NovelEndingSummaryJob 自动生成，也可手动编辑）。
    /// 格式：纯文本，约 200-400 字，描述主要人物的最终走向与结局。
    /// 续写模式下优先注入此摘要，而非 raw chunk。
    /// </summary>
    public string? EndingSummary { get; set; }

    /// <summary>
    /// 文风摘要（由 NovelEndingSummaryJob 顺带提取，也可手动编辑）。
    /// 格式：描述语调、句式、对话风格、描写密度等特征，约 100 字。
    /// 续写/番外模式下注入为文风指导，弥补空白 StyleProfile。
    /// </summary>
    public string? StyleSummary { get; set; }

    /// <summary>结局/文风摘要的最后生成时间，用于判断是否需要重新生成。</summary>
    public DateTime? SummaryGeneratedAt { get; set; }
}
