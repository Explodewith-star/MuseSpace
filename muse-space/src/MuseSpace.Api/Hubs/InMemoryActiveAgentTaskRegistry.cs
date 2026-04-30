using System.Collections.Concurrent;
using MuseSpace.Application.Abstractions.Notifications;

namespace MuseSpace.Api.Hubs;

/// <summary>
/// <see cref="IActiveAgentTaskRegistry"/> 的进程内单例实现。
/// key = (projectId, taskType)。
/// </summary>
public sealed class InMemoryActiveAgentTaskRegistry : IActiveAgentTaskRegistry
{
    private readonly ConcurrentDictionary<(Guid, string), ActiveAgentTaskInfo> _store = new();

    public void Upsert(Guid projectId, string taskType, string stage)
    {
        _store.AddOrUpdate(
            (projectId, taskType),
            _ => new ActiveAgentTaskInfo(taskType, stage, DateTime.UtcNow),
            (_, existing) => existing with { Stage = stage });
    }

    public void Remove(Guid projectId, string taskType)
        => _store.TryRemove((projectId, taskType), out _);

    public IReadOnlyList<ActiveAgentTaskInfo> GetByProject(Guid projectId)
    {
        var list = new List<ActiveAgentTaskInfo>();
        foreach (var kv in _store)
        {
            if (kv.Key.Item1 == projectId) list.Add(kv.Value);
        }
        return list;
    }
}
