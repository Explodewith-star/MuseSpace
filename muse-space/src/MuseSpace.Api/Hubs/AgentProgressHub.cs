using Microsoft.AspNetCore.SignalR;

namespace MuseSpace.Api.Hubs;

/// <summary>
/// SignalR Hub，用于 Agent 任务进度实时推送。
/// 前端连接后调用 JoinProjectGroup(projectId) 订阅特定项目的 Agent 事件。
/// 推送事件：AgentStarted / AgentGenerating / AgentDone / AgentFailed
/// </summary>
public sealed class AgentProgressHub : Hub
{
    public async Task JoinProjectGroup(string projectId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, projectId);

    public async Task LeaveProjectGroup(string projectId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, projectId);
}
