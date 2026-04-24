namespace MuseSpace.Application.Abstractions.Agents;

/// <summary>
/// Agent 可调用的工具接口。每个工具有唯一名称和 JSON Schema 描述。
/// Agent 运行时通过 <see cref="Name"/> 路由到对应工具，传入 JSON 参数并获取 JSON 结果。
/// </summary>
public interface IAgentTool
{
    /// <summary>工具唯一名称，用于 LLM tool_call 路由。命名约定：snake_case。</summary>
    string Name { get; }

    /// <summary>工具的功能描述，会作为 LLM function calling 的 description。</summary>
    string Description { get; }

    /// <summary>
    /// 参数的 JSON Schema 字符串，符合 OpenAI function calling 的 parameters 规范。
    /// 示例：<c>{"type":"object","properties":{"projectId":{"type":"string"}},"required":["projectId"]}</c>
    /// </summary>
    string ParametersSchema { get; }

    /// <summary>
    /// 执行工具调用。
    /// </summary>
    /// <param name="argumentsJson">LLM 产出的 JSON 参数字符串。</param>
    /// <param name="context">Agent 运行上下文（可获取 userId、projectId 等）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>工具执行结果的 JSON 字符串，将回填给 LLM。</returns>
    Task<string> ExecuteAsync(string argumentsJson, AgentRunContext context, CancellationToken cancellationToken = default);
}
