namespace MuseSpace.Application.Abstractions.Notifications;

/// <summary>
/// Agent 任务进度通知抽象。由 Api 层通过 SignalR 实现。
/// </summary>
public interface IAgentProgressNotifier
{
    /// <summary>通知 Agent 任务已开始（正在收集上下文）。</summary>
    Task NotifyStartedAsync(Guid projectId, string taskType, CancellationToken ct = default);

    /// <summary>通知 Agent 正在调用 LLM 生成。</summary>
    Task NotifyGeneratingAsync(Guid projectId, string taskType, CancellationToken ct = default);

    /// <summary>通知 Agent 任务完成。</summary>
    Task NotifyDoneAsync(Guid projectId, string taskType, string summary, CancellationToken ct = default);

    /// <summary>通知 Agent 任务失败。</summary>
    Task NotifyFailedAsync(Guid projectId, string taskType, string error, CancellationToken ct = default);
}
