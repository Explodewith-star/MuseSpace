namespace MuseSpace.Application.Abstractions.Agents;

/// <summary>
/// Agent 定义，描述一个 Agent 的静态配置。
/// 新增 Agent 只需创建一个 AgentDefinition 实例并注册到 DI。
/// </summary>
public sealed class AgentDefinition
{
    /// <summary>Agent 唯一名称，用于路由和日志。命名约定：kebab-case。</summary>
    public required string Name { get; init; }

    /// <summary>Agent 简述（给开发者看的）。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// System Prompt 文本。
    /// 建议从文件加载（如 prompts/agents/xxx-v1.md），此处存运行时值。
    /// </summary>
    public required string SystemPrompt { get; init; }

    /// <summary>该 Agent 可使用的工具名称列表。为空表示纯对话 Agent（不调用工具）。</summary>
    public List<string> ToolNames { get; init; } = [];

    /// <summary>最大推理步数（LLM 调用次数上限），防止死循环。默认 10。</summary>
    public int MaxSteps { get; init; } = 10;
}
