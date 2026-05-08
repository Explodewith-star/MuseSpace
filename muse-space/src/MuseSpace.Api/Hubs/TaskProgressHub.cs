using Microsoft.AspNetCore.SignalR;

namespace MuseSpace.Api.Hubs;

/// <summary>统一后台任务进度 Hub，前端按 userId 加入分组。</summary>
public sealed class TaskProgressHub : Hub
{
    public async Task JoinUserGroup(string userId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

    public async Task LeaveUserGroup(string userId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
}
