using Microsoft.AspNetCore.SignalR;

namespace MuseSpace.Api.Hubs;

/// <summary>
/// SignalR Hub，用于原著导入进度实时推送。
/// 前端连接后调用 JoinNovelGroup(novelId) 订阅特定小说的进度事件。
/// 推送事件：ChunkProgress / EmbedProgress / ImportDone / ImportFailed
/// </summary>
public sealed class NovelImportHub : Hub
{
    public async Task JoinNovelGroup(string novelId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, novelId);

    public async Task LeaveNovelGroup(string novelId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, novelId);
}
