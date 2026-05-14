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

    public Task NotifyStartedAsync(Guid projectId, string taskType, AgentProgressDetails? details = null, CancellationToken ct = default)
    {
        _registry.Upsert(projectId, taskType, "started");
        return _hub.Clients.Group(projectId.ToString())
            .SendAsync("AgentStarted", BuildPayload(projectId, taskType, "started", details: details), ct);
    }

    public Task NotifyGeneratingAsync(Guid projectId, string taskType, AgentProgressDetails? details = null, CancellationToken ct = default)
    {
        _registry.Upsert(projectId, taskType, "generating");
        return _hub.Clients.Group(projectId.ToString())
            .SendAsync("AgentGenerating", BuildPayload(projectId, taskType, "generating", details: details), ct);
    }

    public Task NotifyDoneAsync(Guid projectId, string taskType, string summary, AgentProgressDetails? details = null, CancellationToken ct = default)
    {
        _registry.Remove(projectId, taskType);
        return _hub.Clients.Group(projectId.ToString())
            .SendAsync("AgentDone", BuildPayload(projectId, taskType, "done", summary: summary, details: details), ct);
    }

    public Task NotifyFailedAsync(Guid projectId, string taskType, string error, AgentProgressDetails? details = null, CancellationToken ct = default)
    {
        _registry.Remove(projectId, taskType);
        return _hub.Clients.Group(projectId.ToString())
            .SendAsync("AgentFailed", BuildPayload(projectId, taskType, "failed", error: error, details: details), ct);
    }

    private static object BuildPayload(
        Guid projectId,
        string taskType,
        string stage,
        string? summary = null,
        string? error = null,
        AgentProgressDetails? details = null)
    {
        return new
        {
            projectId,
            taskType,
            stage,
            summary,
            error,
            novelId = details?.NovelId,
            assets = details?.Assets,
        };
    }
}
