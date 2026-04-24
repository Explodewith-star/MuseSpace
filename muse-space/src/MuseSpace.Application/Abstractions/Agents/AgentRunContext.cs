namespace MuseSpace.Application.Abstractions.Agents;

/// <summary>
/// Agent 运行上下文，贯穿整个 Agent 执行周期。
/// 工具可通过此对象获取当前用户、项目等信息。
/// </summary>
public sealed class AgentRunContext
{
    /// <summary>当前用户 ID（从 JWT 获取），null 表示游客。</summary>
    public Guid? UserId { get; init; }

    /// <summary>关联的故事项目 ID（可选）。</summary>
    public Guid? ProjectId { get; init; }

    /// <summary>本次 Agent 运行的唯一标识。</summary>
    public Guid RunId { get; init; } = Guid.NewGuid();

    /// <summary>累计输入 token 数（由 AgentRunner 更新）。</summary>
    public int TotalInputTokens { get; set; }

    /// <summary>累计输出 token 数（由 AgentRunner 更新）。</summary>
    public int TotalOutputTokens { get; set; }

    /// <summary>当前步骤数。</summary>
    public int CurrentStep { get; set; }

    /// <summary>附加参数，由调用方按需填充。</summary>
    public Dictionary<string, string> Parameters { get; init; } = [];
}
