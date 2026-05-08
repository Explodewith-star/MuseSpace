using Microsoft.AspNetCore.SignalR;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Enums;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Api.Hubs;

public sealed class TaskProgressService : ITaskProgressService
{
    private readonly IBackgroundTaskRepository _repo;
    private readonly IHubContext<TaskProgressHub> _hub;

    public TaskProgressService(IBackgroundTaskRepository repo, IHubContext<TaskProgressHub> hub)
    {
        _repo = repo;
        _hub = hub;
    }

    public async Task<Guid> StartAsync(Guid? userId, Guid? projectId, BackgroundTaskType taskType, string title, CancellationToken ct = default)
    {
        var record = new BackgroundTaskRecord
        {
            UserId = userId,
            StoryProjectId = projectId,
            TaskType = taskType,
            Status = BackgroundTaskStatus.Running,
            Title = title,
            StatusMessage = "任务已启动",
        };
        await _repo.AddAsync(record, ct);
        if (userId.HasValue)
            await SendToUserAsync(userId.Value, "TaskStarted", ToPayload(record), ct);
        return record.Id;
    }

    public async Task ReportProgressAsync(Guid taskId, int progress, string? statusMessage = null, CancellationToken ct = default)
    {
        var record = await _repo.GetByIdAsync(taskId, ct);
        if (record is null) return;

        record.Progress = Math.Clamp(progress, 0, 100);
        if (statusMessage is not null) record.StatusMessage = statusMessage;
        record.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(record, ct);

        if (record.UserId.HasValue)
            await SendToUserAsync(record.UserId.Value, "TaskProgress", ToPayload(record), ct);
    }

    public async Task CompleteAsync(Guid taskId, string? message = null, CancellationToken ct = default)
    {
        var record = await _repo.GetByIdAsync(taskId, ct);
        if (record is null) return;

        record.Status = BackgroundTaskStatus.Completed;
        record.Progress = 100;
        record.StatusMessage = message ?? "已完成";
        record.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(record, ct);

        if (record.UserId.HasValue)
            await SendToUserAsync(record.UserId.Value, "TaskCompleted", ToPayload(record), ct);
    }

    public async Task FailAsync(Guid taskId, string error, CancellationToken ct = default)
    {
        var record = await _repo.GetByIdAsync(taskId, ct);
        if (record is null) return;

        record.Status = BackgroundTaskStatus.Failed;
        record.ErrorMessage = error;
        record.StatusMessage = "任务失败";
        record.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(record, ct);

        if (record.UserId.HasValue)
            await SendToUserAsync(record.UserId.Value, "TaskFailed", ToPayload(record), ct);
    }

    private Task SendToUserAsync(Guid userId, string method, object payload, CancellationToken ct)
        => _hub.Clients.Group($"user-{userId}").SendAsync(method, payload, ct);

    private static object ToPayload(BackgroundTaskRecord r) => new
    {
        id = r.Id,
        taskType = r.TaskType.ToString(),
        status = r.Status.ToString(),
        progress = r.Progress,
        title = r.Title,
        statusMessage = r.StatusMessage,
        errorMessage = r.ErrorMessage,
        createdAt = r.CreatedAt,
        updatedAt = r.UpdatedAt,
    };
}
