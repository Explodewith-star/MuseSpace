using MuseSpace.Domain.Enums;

namespace MuseSpace.Application.Abstractions.Notifications;

/// <summary>
/// 统一后台任务进度跟踪服务。负责持久化任务记录 + SignalR 实时推送。
/// </summary>
public interface ITaskProgressService
{
    /// <summary>创建任务记录并通知前端。返回任务 ID。</summary>
    Task<Guid> StartAsync(Guid? userId, Guid? projectId, BackgroundTaskType taskType, string title, CancellationToken ct = default);

    /// <summary>更新进度（百分比 + 状态消息）。</summary>
    Task ReportProgressAsync(Guid taskId, int progress, string? statusMessage = null, CancellationToken ct = default);

    /// <summary>标记任务完成。</summary>
    Task CompleteAsync(Guid taskId, string? message = null, CancellationToken ct = default);

    /// <summary>标记任务失败。</summary>
    Task FailAsync(Guid taskId, string error, CancellationToken ct = default);
}
