using Microsoft.AspNetCore.SignalR;
using MuseSpace.Application.Abstractions.Notifications;

namespace MuseSpace.Api.Hubs;

/// <summary>
/// IAgentProgressNotifier 的 SignalR 实现。
/// 按 projectId 分组推送 Agent 任务状态变化。
/// </summary>
public sealed class SignalRAgentProgressNotifier : IAgentProgressNotifier
{
    private readonly IHubContext<AgentProgressHub> _hub;
    private readonly IActiveAgentTaskRegistry _registry;

    public SignalRAgentProgressNotifier(IHubContext<AgentProgressHub> hub, IActiveAgentTaskRegistry registry)
    {
        _hub = hub;
        _registry = registry;
    }

    public Task NotifyStartedAsync(Guid projectId, string taskType, CancellationToken ct = default)
    {
        _registry.Upsert(projectId, taskType, "started");
        return _hub.Clients.Group(projectId.ToString())
            .SendAsync("AgentStarted", new { projectId, taskType, stage = "started" }, ct);
    }

    public Task NotifyGeneratingAsync(Guid projectId, string taskType, CancellationToken ct = default)
    {
        _registry.Upsert(projectId, taskType, "generating");
        return _hub.Clients.Group(projectId.ToString())
            .SendAsync("AgentGenerating", new { projectId, taskType, stage = "generating" }, ct);
    }

    public Task NotifyDoneAsync(Guid projectId, string taskType, string summary, CancellationToken ct = default)
    {
        _registry.Remove(projectId, taskType);
        return _hub.Clients.Group(projectId.ToString())
            .SendAsync("AgentDone", new { projectId, taskType, stage = "done", summary }, ct);
    }

    public Task NotifyFailedAsync(Guid projectId, string taskType, string error, CancellationToken ct = default)
    {
        _registry.Remove(projectId, taskType);
        return _hub.Clients.Group(projectId.ToString())
            .SendAsync("AgentFailed", new { projectId, taskType, stage = "failed", error }, ct);
    }
}
