using MuseSpace.Application.Abstractions.Suggestions;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Services.Suggestions;

/// <summary>
/// 通用 "仅知晓 / 不落表" Applier 基类。
/// 一致性冲突、项目摘要等纯通知类建议的 Apply 等同于"用户已知晓"。
/// </summary>
public abstract class AcknowledgeOnlySuggestionApplierBase : ISuggestionApplier
{
    public abstract string Category { get; }

    public Task<Guid> ApplyAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default)
        => Task.FromResult(suggestion.Id);

    public Task RetractAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>世界观冲突建议（一致性审查）。</summary>
public sealed class WorldRuleConsistencyApplier : AcknowledgeOnlySuggestionApplierBase
{
    public override string Category => SuggestionCategories.WorldRuleConsistency;
}

/// <summary>角色行为/对话冲突建议。</summary>
public sealed class CharacterConsistencyApplier : AcknowledgeOnlySuggestionApplierBase
{
    public override string Category => SuggestionCategories.CharacterConsistency;
}

/// <summary>文风偏离建议。</summary>
public sealed class StyleConsistencyApplier : AcknowledgeOnlySuggestionApplierBase
{
    public override string Category => SuggestionCategories.StyleConsistency;
}

/// <summary>大纲与世界观冲突预检建议。</summary>
public sealed class OutlineConsistencyApplier : AcknowledgeOnlySuggestionApplierBase
{
    public override string Category => SuggestionCategories.OutlineConsistency;
}

/// <summary>项目摘要 / 写作进展通知建议。</summary>
public sealed class ProjectSummaryApplier : AcknowledgeOnlySuggestionApplierBase
{
    public override string Category => SuggestionCategories.ProjectSummary;
}

/// <summary>伏笔追踪通知建议（v1：仅知晓；后续可扩展为自动改写）。</summary>
public sealed class PlotThreadSuggestionApplier : AcknowledgeOnlySuggestionApplierBase
{
    public override string Category => SuggestionCategories.PlotThread;
}

/// <summary>
/// 兼容老数据的通用一致性 Applier；当老建议尚未通过启动迁移拆分到子类目时仍可被 Apply。
/// </summary>
#pragma warning disable CS0618 // Obsolete category
public sealed class LegacyConsistencySuggestionApplier : AcknowledgeOnlySuggestionApplierBase
{
    public override string Category => SuggestionCategories.Consistency;
}
#pragma warning restore CS0618

