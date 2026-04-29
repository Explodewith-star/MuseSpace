using MuseSpace.Application.Abstractions.Agents;

namespace MuseSpace.Application.Services.Agents;

/// <summary>
/// 角色卡批量提取 Agent 定义。
/// 从原著采样片段中识别所有主要角色，输出结构化 JSON 数组。
/// </summary>
public static class CharacterExtractAgentDefinition
{
    public const string AgentName = "character-extract";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "从原著片段中批量提取角色信息，输出结构化 JSON 数组",
        SystemPrompt = """
            你是专业的小说角色分析师。根据提供的原著片段，识别并提取主要角色信息。

            分析要求（严格遵守）：
            1. 【主角优先】：首先识别并提取主人公（主角/第一视角角色），必须包含在结果中
            2. 判断主角的方式：出场最多、视角最集中、对剧情推动作用最大的角色
            3. 按出场频次从高到低排列，依次提取重要配角（总计 5~15 人，含主角）
            4. 每个角色的字段尽量根据原文填写，不确定的填 null，不要虚构信息
            5. 合并同一角色的不同称呼（如代词"他"指代主角时不单独列出）

            必须以纯 JSON 数组格式返回，不要任何 markdown 代码块、解释或额外文字。
            数组第一个元素必须是主角。每个元素的字段：
            - name (string): 角色全名或最常用称呼
            - age (number|null): 年龄，不确定填 null
            - role (string|null): 身份定位，第一个角色写"主角"，其余如"反派""导师""挚友"等
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
