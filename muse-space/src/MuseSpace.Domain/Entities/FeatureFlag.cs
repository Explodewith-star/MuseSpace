namespace MuseSpace.Domain.Entities;

/// <summary>
/// 功能开关：用于灰度上线 / 紧急关闭某些链路（如自动伏笔追踪、导入时自动提取等）。
/// </summary>
public class FeatureFlag
{
    /// <summary>唯一键，如 "auto-plot-thread-tracking"。</summary>
    public string Key { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
