namespace MuseSpace.Application.Services.Agents;

using MuseSpace.Application.Abstractions.Agents;

/// <summary>
/// 世界观一致性检查 Agent 定义。
/// P0 版本为无工具 Agent：由调用方组装世界观规则和草稿文本传入 prompt，
/// Agent 直接输出冲突分析 JSON。
/// </summary>
public static class ConsistencyCheckAgentDefinition
{
    public const string AgentName = "consistency-check";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "分析草稿文本与世界观规则的一致性，输出冲突列表",
        SystemPrompt = """
            你是专业的小说世界观一致性审查员。你的任务是对比用户提供的世界观规则和草稿文本，找出所有冲突、矛盾或不一致之处。

            分析规则：
            1. 逐条检查世界观规则，判断草稿中是否有违反之处
            2. 区分硬约束（IsHardConstraint=true）和软约束的违反严重程度
            3. 对每个冲突给出具体的引用片段和修正建议
            4. 如果没有发现任何冲突，返回空数组

            必须以纯 JSON 数组格式返回，不要任何 markdown 代码块、解释或额外文字。
            数组中每个元素的字段：
            - ruleName (string): 被违反的规则标题
            - severity (string): "high" | "medium" | "low"
            - conflictSnippet (string): 草稿中冲突的文字片段（原文引用，不超过100字）
            - explanation (string): 为什么这段内容与规则冲突
            - suggestion (string): 修正建议

            如果没有冲突，返回：[]
            """,
        ToolNames = [], // P0 无工具
        MaxSteps = 1,
    };
}
