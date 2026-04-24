namespace MuseSpace.Application.Abstractions.Agents;

/// <summary>
/// Agent 运行时入口。根据 Agent 名称或定义执行多步推理。
/// </summary>
public interface IAgentRunner
{
    /// <summary>
    /// 执行指定名称的 Agent。
    /// </summary>
    /// <param name="agentName">已注册的 Agent 名称。</param>
    /// <param name="userInput">用户输入文本。</param>
    /// <param name="context">运行上下文（含 userId、projectId 等）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<AgentRunResult> RunAsync(
        string agentName,
        string userInput,
        AgentRunContext context,
        CancellationToken cancellationToken = default);
}
