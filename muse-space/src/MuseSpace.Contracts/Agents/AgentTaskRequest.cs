namespace MuseSpace.Contracts.Agents;

/// <summary>
/// 菜单 Agent 化（D3-2）的统一触发入参。
/// 通过 <see cref="AgentType"/> 路由到具体的 Hangfire Job。
/// </summary>
public sealed class AgentTaskRequest
{
    /// <summary>
    /// 目标 Agent 类型，与各 AgentDefinition.AgentName 对齐：
    /// 资产提取：character-extract / worldrule-extract / styleprofile-extract / extract-all。
    /// 一致性审查：consistency-check / character-consistency / style-consistency。
    /// 章节规划：chapter-auto-plan。
    /// 项目摘要：project-summary。
    /// </summary>
    public string AgentType { get; set; } = string.Empty;

    /// <summary>用户的一句话目标或补充约束。可选。</summary>
    public string? UserInput { get; set; }

    /// <summary>指定原著 ID（提取类）。为 null 时后端默认选项目内最近完成索引的原著。</summary>
    public Guid? NovelId { get; set; }

    /// <summary>指定章节 ID（一致性审查 / 章节自动规划）。当 <see cref="Scope"/>=latest-draft 时必填。</summary>
    public Guid? ChapterId { get; set; }

    /// <summary>原始文本，当 <see cref="Scope"/>=raw-text 时必填。</summary>
    public string? RawText { get; set; }

    /// <summary>
    /// 一致性审查的文本来源：
    /// latest-draft（默认）= 取 ChapterId 对应章节的 DraftText；
    /// raw-text = 直接使用 <see cref="RawText"/>；
    /// all-drafts = 拼接项目所有章节的 DraftText（自动截断防超长）。
    /// </summary>
    public string? Scope { get; set; }
}

public sealed class AgentTaskResponse
{
    public string TaskId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    /// <summary>SignalR 推送的 taskType，前端用于过滤进度事件。</summary>
    public string TaskType { get; set; } = string.Empty;
}

