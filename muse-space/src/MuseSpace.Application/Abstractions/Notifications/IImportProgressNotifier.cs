namespace MuseSpace.Application.Abstractions.Notifications;

/// <summary>
/// 导入进度通知抽象。由 Api 层通过 SignalR 实现，Infrastructure Jobs 仅依赖此接口。
/// </summary>
public interface IImportProgressNotifier
{
    Task NotifyChunkingProgressAsync(Guid novelId, int done, int total, CancellationToken ct = default);
    Task NotifyEmbedProgressAsync(Guid novelId, int done, int total, CancellationToken ct = default);
    Task NotifyImportDoneAsync(Guid novelId, int total, CancellationToken ct = default);
    Task NotifyImportFailedAsync(Guid novelId, string error, CancellationToken ct = default);
}
