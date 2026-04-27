using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Suggestions;

/// <summary>
/// 将已接受的 AgentSuggestion 写入正式业务表。
/// 每种 Category 提供自己的实现。
/// </summary>
public interface ISuggestionApplier
{
    /// <summary>该 Applier 处理的建议类型。</summary>
    string Category { get; }

    /// <summary>
    /// 把建议内容写入正式业务表，返回新建/更新的实体 ID。
    /// </summary>
    Task<Guid> ApplyAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default);
}
