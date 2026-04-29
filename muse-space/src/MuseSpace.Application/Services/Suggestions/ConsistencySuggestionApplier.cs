using MuseSpace.Application.Abstractions.Suggestions;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Services.Suggestions;

/// <summary>
/// 一致性冲突建议的 Apply 逻辑。
/// 与 Character 不同，一致性建议的 "Apply" 不是写入新表，
/// 而是标记为"已确认知晓的冲突"——后续版本可扩展为自动修正。
/// </summary>
public sealed class ConsistencySuggestionApplier : ISuggestionApplier
{
    public string Category => SuggestionCategories.Consistency;

    public Task<Guid> ApplyAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        // P0 版本：一致性建议的 Apply 仅表示用户确认已知晓此冲突。
        // 不写入额外业务表，返回建议自身 ID 作为标记。
        // 后续 D4 可扩展为触发改写 Agent 自动修正。
        return Task.FromResult(suggestion.Id);
    }

    /// <summary>一致性建议无对应资产实体，撤回时无需删除任何数据。</summary>
    public Task RetractAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
