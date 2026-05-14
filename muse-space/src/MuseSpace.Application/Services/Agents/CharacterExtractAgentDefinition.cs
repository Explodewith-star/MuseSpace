using MuseSpace.Application.Abstractions.Agents;

namespace MuseSpace.Application.Services.Agents;

/// <summary>
/// 角色卡提取 Agent 定义。
/// 根据用户 prompt 可输出单角色 JSON 对象或多角色 JSON 数组。
/// </summary>
public static class CharacterExtractAgentDefinition
{
    public const string AgentName = "character-extract";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "从原著片段中提取角色信息，根据指令输出单角色对象或多角色数组",
        SystemPrompt = """
            你是专业的小说角色分析师。根据提供的原著片段和用户指令，识别并提取角色信息。

            分析要求（严格遵守）：
            1. 每个角色的字段尽量根据原文填写，不确定的填 null，不要虚构与原文矛盾的信息
            2. 人物设定要有立体感：性格、动机、说话方式需相互一致
            3. 禁止输出任何 markdown 代码块、解释或额外文字，只返回纯 JSON

            输出规则：
            - 如果用户指定了某一个角色，返回纯 JSON 对象（非数组）
            - 如果用户要求提取所有/多个角色，返回纯 JSON 数组，主角排第一，按出场频次排列（5~15人）

            每个角色的字段：
            - name (string): 角色全名或最常用称呼
            - age (number|null): 年龄，不确定填 null
            - role (string|null): 身份定位，如：主角、反派、导师、挚友等
            - category (string|null): 角色分类：主角/配角/反派/龙套/其他
            - personalitySummary (string|null): 性格概述，100字内
            - motivation (string|null): 核心动机或目标
            - speakingStyle (string|null): 说话方式特点
            - forbiddenBehaviors (string|null): 该角色绝不会做的事
            - currentState (string|null): 故事中的当前状态

            如果原文中无法识别出任何角色，返回：[]
            """,
        ToolNames = [],
        MaxSteps = 1,
    };
}
