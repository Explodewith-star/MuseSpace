using Microsoft.AspNetCore.SignalR;
using MuseSpace.Application.Abstractions.Notifications;

namespace MuseSpace.Api.Hubs;

/// <summary>
/// IImportProgressNotifier 的 SignalR 实现。
/// Infrastructure Jobs 注入接口，Api 层提供具体实现，避免循环依赖。
/// </summary>
public sealed class SignalRImportProgressNotifier : IImportProgressNotifier
{
    private readonly IHubContext<NovelImportHub> _hub;

    public SignalRImportProgressNotifier(IHubContext<NovelImportHub> hub)
        => _hub = hub;

    public Task NotifyChunkingDoneAsync(Guid novelId, int totalChunks, CancellationToken ct = default)
        => _hub.Clients.Group(novelId.ToString())
            .SendAsync("ChunkProgress",
                new { novelId, stage = "chunking", done = totalChunks, total = totalChunks }, ct);

    public Task NotifyEmbedProgressAsync(Guid novelId, int done, int total, CancellationToken ct = default)
        => _hub.Clients.Group(novelId.ToString())
            .SendAsync("EmbedProgress",
                new { novelId, stage = "embedding", done, total }, ct);

    public Task NotifyImportDoneAsync(Guid novelId, CancellationToken ct = default)
        => _hub.Clients.Group(novelId.ToString())
            .SendAsync("ImportDone", new { novelId, status = "success" }, ct);

    public Task NotifyImportFailedAsync(Guid novelId, string error, CancellationToken ct = default)
        => _hub.Clients.Group(novelId.ToString())
            .SendAsync("ImportFailed", new { novelId, error }, ct);
}
