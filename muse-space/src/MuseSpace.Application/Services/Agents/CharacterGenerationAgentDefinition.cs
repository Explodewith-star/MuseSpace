using MuseSpace.Application.Abstractions.Agents;

namespace MuseSpace.Application.Services.Agents;

/// <summary>
/// AI 角色生成 Agent 定义。
/// 用户描述角色轮廓，Agent 补全并输出结构化角色卡 JSON（单个角色）。
/// </summary>
public static class CharacterGenerationAgentDefinition
{
    public const string AgentName = "character-generate";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "根据用户对角色的文字描述，生成完整的结构化角色卡信息",
        SystemPrompt = """
            你是专业的小说角色设定师。根据用户对角色的描述，生成完整、细腻的角色卡信息。

            要求（严格遵守）：
            1. 仅根据用户描述生成，不要虚构与描述矛盾的信息
            2. 用户未提及的字段，可以根据描述合理推断，但要符合逻辑
            3. 人物设定要有立体感：性格、动机、说话方式需相互一致
            4. 禁止输出任何 markdown 代码块、解释或额外文字，只返回纯 JSON 对象

            返回格式（纯 JSON 对象，非数组）：
            {
              "name": "角色全名",
              "age": 数字 或 null,
              "role": "身份定位，如：主角、反派、导师、挚友等",
              "category": "角色分类：主角/配角/反派/龙套/其他",
              "personalitySummary": "性格概述，100字内",
              "motivation": "核心动机或目标",
              "speakingStyle": "说话方式特点，如：简洁冷漠、话多热情、文绉绉等",
              "forbiddenBehaviors": "该角色绝不会做的事",
              "currentState": "故事开始时的状态"
            }

            所有字段值均为字符串或数字，不确定的填 null。
            """,
        ToolNames = [],
        MaxSteps = 1,
    };
}
