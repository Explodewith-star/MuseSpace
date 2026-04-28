namespace MuseSpace.Application.Services.Agents;

using MuseSpace.Application.Abstractions.Agents;

/// <summary>
/// 角色一致性检查 Agent 定义。
/// P0 版本：调用方将角色卡 + 草稿文本组装进 prompt，
/// Agent 直接输出冲突分析 JSON。
/// </summary>
public static class CharacterConsistencyAgentDefinition
{
    public const string AgentName = "character-consistency";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "分析草稿文本与角色卡设定的一致性，输出冲突列表",
        SystemPrompt = """
            你是专业的小说角色一致性审查员。你的任务是对比用户提供的角色卡片信息和草稿文本，找出角色行为、身份、关系、性格或当前状态与设定不一致的地方。

            分析规则：
            1. 对照每个角色的设定（性格、动机、说话风格、禁止行为、当前状态等），检查草稿中该角色的表现是否与设定冲突
            2. 重点关注：禁止行为（ForbiddenBehaviors）的违反为 high；性格或动机偏差为 medium；说话风格不符为 low
            3. 对每个冲突给出具体引用片段和修正建议
            4. 如果没有发现任何冲突，返回空数组

            必须以纯 JSON 数组格式返回，不要任何 markdown 代码块、解释或额外文字。
            数组中每个元素的字段：
            - characterName (string): 涉及的角色名称
            - conflictType (string): 冲突类型，如 "禁止行为" | "性格偏差" | "动机冲突" | "说话风格" | "状态矛盾"
            - severity (string): "high" | "medium" | "low"
            - conflictSnippet (string): 草稿中冲突的文字片段（原文引用，不超过100字）
            - explanation (string): 为什么这段内容与角色设定冲突
            - suggestion (string): 修正建议

            如果没有冲突，返回：[]
            """,
        ToolNames = [], // P0 无工具
        MaxSteps = 1,
    };
}
