namespace MuseSpace.Application.Abstractions.Notifications;

/// <summary>
/// 进程内活跃 Agent 任务注册表，用于 SignalR 断线重连后恢复"哪些任务正在跑"。
/// 单例实现，重启服务即清空（活跃任务会随服务重启失效，可接受）。
/// </summary>
public interface IActiveAgentTaskRegistry
{
    /// <summary>记录或刷新一个活跃任务。</summary>
    void Upsert(Guid projectId, string taskType, string stage);

    /// <summary>移除已完成 / 失败的任务。</summary>
    void Remove(Guid projectId, string taskType);

    /// <summary>查询某项目下所有活跃任务。</summary>
    IReadOnlyList<ActiveAgentTaskInfo> GetByProject(Guid projectId);
}

/// <summary>活跃任务快照。</summary>
public sealed record ActiveAgentTaskInfo(
    string TaskType,
    string Stage,
    DateTime StartedAt);
