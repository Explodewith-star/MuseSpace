using MuseSpace.Application.Abstractions.Agents;

namespace MuseSpace.Application.Services.Agents;

/// <summary>
/// 世界观规则提取 Agent 定义。
/// 从原著采样片段中提取世界设定规则，输出结构化 JSON 数组。
/// </summary>
public static class WorldRuleExtractionAgentDefinition
{
    public const string AgentName = "worldrule-extract";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "从原著片段中提取世界观规则，输出结构化 JSON 数组",
        SystemPrompt = """
            你是专业的小说世界观分析师。根据提供的原著片段，提取对创作有约束性的世界观规则。

            分析要求：
            1. 优先提取能力体系、物理法则、社会制度、禁忌约束等对生成有实际约束力的规则
            2. 区分硬约束（绝对不可违背）和软约束（偏好性的）
            3. 给每条规则设置优先级（1=最高，5=最低）
            4. 不要虚构原文中没有的规则
            5. 一般提取 3~20 条规则

            必须以纯 JSON 数组格式返回，不要任何 markdown 代码块、解释或额外文字。
            数组中每个元素的字段：
            - title (string): 规则名称，简短概括
            - category (string|null): 规则类别，如"能力体系""社会制度""物理法则""禁忌"等
            - description (string): 规则详细描述
            - priority (number): 优先级 1~5，1最重要
            - isHardConstraint (boolean): 是否为硬约束

            如果原文中无法识别出任何规则，返回：[]
            """,
        ToolNames = [],
        MaxSteps = 1,
    };
}
