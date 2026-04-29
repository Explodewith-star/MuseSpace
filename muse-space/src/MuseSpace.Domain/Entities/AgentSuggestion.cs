using MuseSpace.Domain.Enums;

namespace MuseSpace.Domain.Entities;

/// <summary>
/// Agent 产出的候选建议，统一承载角色、世界观、大纲、一致性等各类 Agent 结果。
/// 所有建议共享同一套 Pending → Accepted → Applied / Ignored 状态流转。
/// </summary>
public class AgentSuggestion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>关联的 AgentRun ID，标记由哪次 Agent 执行产出。</summary>
    public Guid AgentRunId { get; set; }

    /// <summary>所属故事项目。</summary>
    public Guid StoryProjectId { get; set; }

    /// <summary>建议类型：Character / WorldRule / Outline / StyleProfile / Consistency 等。</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>建议标题，如"候选角色：张三""世界观冲突：剑法设定矛盾"。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>建议正文，JSON 格式。不同 Category 对应不同 schema，由 Application 层 DTO 校验。</summary>
    public string ContentJson { get; set; } = "{}";

    /// <summary>建议状态。</summary>
    public SuggestionStatus Status { get; set; } = SuggestionStatus.Pending;

    /// <summary>
    /// 来源原著 ID（仅资产提取类建议填写，一致性检查/大纲等为 null）。
    /// 用于删除原著时精确清理本书产出的未应用建议，与其他书隔离。
    /// </summary>
    public Guid? SourceNovelId { get; set; }

    /// <summary>应用目标实体 ID（如已有角色的 Id），null 表示新建。</summary>
    public Guid? TargetEntityId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>接受 / 忽略的时间。</summary>
    public DateTime? ResolvedAt { get; set; }
}
