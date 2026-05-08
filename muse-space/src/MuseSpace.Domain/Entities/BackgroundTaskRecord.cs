using MuseSpace.Domain.Enums;

namespace MuseSpace.Domain.Entities;

/// <summary>后台任务跟踪记录</summary>
public class BackgroundTaskRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public Guid? StoryProjectId { get; set; }
    public BackgroundTaskType TaskType { get; set; }
    public BackgroundTaskStatus Status { get; set; } = BackgroundTaskStatus.Pending;

    /// <summary>进度百分比 0-100</summary>
    public int Progress { get; set; }

    /// <summary>当前步骤描述，如"正在分析角色关系..."</summary>
    public string? StatusMessage { get; set; }

    /// <summary>任务显示标题，如"导入《xxx》"</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>失败时的错误信息</summary>
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
