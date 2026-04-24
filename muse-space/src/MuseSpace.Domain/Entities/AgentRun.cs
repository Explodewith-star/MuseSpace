namespace MuseSpace.Domain.Entities;

/// <summary>
/// Agent 运行记录实体，每次 Agent 执行落一行。
/// 用于可观测（成功率、token 消耗、耗时）和审计追溯。
/// </summary>
public class AgentRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Agent 名称（如 "character-extract"）。</summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>发起用户 ID，null 表示系统触发。</summary>
    public Guid? UserId { get; set; }

    /// <summary>关联的故事项目 ID（可选）。</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>运行状态。</summary>
    public AgentRunStatus Status { get; set; } = AgentRunStatus.Running;

    /// <summary>执行步数。</summary>
    public int StepCount { get; set; }

    /// <summary>累计输入 token。</summary>
    public int InputTokens { get; set; }

    /// <summary>累计输出 token。</summary>
    public int OutputTokens { get; set; }

    /// <summary>执行耗时（毫秒）。</summary>
    public long DurationMs { get; set; }

    /// <summary>用户输入摘要（截断保存）。</summary>
    public string? InputPreview { get; set; }

    /// <summary>Agent 最终输出摘要（截断保存）。</summary>
    public string? OutputPreview { get; set; }

    /// <summary>失败时的错误信息。</summary>
    public string? ErrorMessage { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
}

public enum AgentRunStatus
{
    Running = 0,
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3,
}
