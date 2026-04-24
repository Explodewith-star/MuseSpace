namespace MuseSpace.Application.Abstractions.Agents;

/// <summary>
/// Agent 执行结果。
/// </summary>
public sealed class AgentRunResult
{
    /// <summary>是否成功完成（LLM 给出了最终输出且未超时/超步）。</summary>
    public bool Success { get; init; }

    /// <summary>Agent 最终输出文本。</summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>失败时的错误信息。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Agent 名称。</summary>
    public string AgentName { get; init; } = string.Empty;

    /// <summary>执行步骤列表。</summary>
    public List<AgentStep> Steps { get; init; } = [];

    /// <summary>总执行毫秒数。</summary>
    public long DurationMs { get; init; }

    /// <summary>累计输入 token。</summary>
    public int TotalInputTokens { get; init; }

    /// <summary>累计输出 token。</summary>
    public int TotalOutputTokens { get; init; }
}
