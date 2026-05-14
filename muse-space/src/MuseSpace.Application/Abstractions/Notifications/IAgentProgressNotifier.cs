namespace MuseSpace.Application.Abstractions.Notifications;

public sealed class AgentProgressDetails
{
    public Guid? NovelId { get; init; }

    public IReadOnlyList<AgentProgressAssetResult>? Assets { get; init; }
}

public sealed class AgentProgressAssetResult
{
    public required string AssetType { get; init; }

    public required string Label { get; init; }

    public required string Status { get; init; }

    public string? Message { get; init; }

    public string? RetryAgentType { get; init; }
}

/// <summary>
/// Agent 任务进度通知抽象。由 Api 层通过 SignalR 实现。
/// </summary>
public interface IAgentProgressNotifier
{
    /// <summary>通知 Agent 任务已开始（正在收集上下文）。</summary>
    Task NotifyStartedAsync(Guid projectId, string taskType, AgentProgressDetails? details = null, CancellationToken ct = default);

    /// <summary>通知 Agent 正在调用 LLM 生成。</summary>
    Task NotifyGeneratingAsync(Guid projectId, string taskType, AgentProgressDetails? details = null, CancellationToken ct = default);

    /// <summary>通知 Agent 任务完成。</summary>
    Task NotifyDoneAsync(Guid projectId, string taskType, string summary, AgentProgressDetails? details = null, CancellationToken ct = default);

    /// <summary>通知 Agent 任务失败。</summary>
    Task NotifyFailedAsync(Guid projectId, string taskType, string error, AgentProgressDetails? details = null, CancellationToken ct = default);
}
