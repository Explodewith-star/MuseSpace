namespace MuseSpace.Application.Abstractions.Agents;

/// <summary>
/// Agent 单步执行记录。
/// </summary>
public sealed class AgentStep
{
    /// <summary>步骤序号（从 1 开始）。</summary>
    public int Index { get; init; }

    /// <summary>步骤类型。</summary>
    public AgentStepType Type { get; init; }

    /// <summary>LLM 原始输出 / 工具名 / 工具结果（按类型不同存不同内容）。</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>工具名称（仅 ToolCall / ToolResult 有值）。</summary>
    public string? ToolName { get; init; }

    /// <summary>工具参数 JSON（仅 ToolCall 有值）。</summary>
    public string? ToolArguments { get; init; }
}

public enum AgentStepType
{
    /// <summary>LLM 思考/输出步骤。</summary>
    LlmResponse,

    /// <summary>LLM 发起工具调用。</summary>
    ToolCall,

    /// <summary>工具执行返回结果。</summary>
    ToolResult,
}
