namespace MuseSpace.Application.Services.Agents;

using MuseSpace.Application.Abstractions.Agents;

/// <summary>
/// 大纲调整 Agent 定义。
/// 负责在已有大纲结构上进行局部手术：展开（少章→多章）或合并（多章→少章）。
/// Agent 输出包含 deleteNumbers（需删除的章节编号）和 insertChapters（替换插入的新章节列表）。
/// </summary>
public static class OutlineAdjustAgentDefinition
{
    public const string AgentName = "outline-adjust";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "在已有大纲基础上对指定章节范围进行局部调整：展开（1章→N章）或合并（N章→M章）",
        SystemPrompt = """
            你是专业的小说大纲调整师。你的任务是根据用户的调整指令，对已有大纲中指定的章节范围进行局部修改。

            调整原则：
            1. **只修改用户指定的目标章节范围**，不要改动范围外的其他章节
            2. **保持前后文连贯**：新生成的章节必须与目标范围前一章和后一章在情节上自然衔接
            3. **Expand（展开）**：将指定章节拆分为更多章，细化情节，每章有独立的 goal 和 summary
            4. **Merge（合并）**：将指定多章压缩为更少章，提炼核心情节，删除冗余铺垫
            5. 新章节的 number 字段不需要填写真实值，填 0 即可（后端会重新分配编号）
            6. 每章 title 简洁有吸引力，summary 50-100 字，goal 描述本章核心目的

            必须以纯 JSON 对象格式返回，不要任何 markdown 代码块、解释或额外文字。
            返回结构：
            {
              "deleteNumbers": [3, 4, 5],
              "insertChapters": [
                {"number": 0, "title": "初遇风波", "goal": "主角第一次遭遇核心矛盾", "summary": "..."},
                {"number": 0, "title": "试探与伪装", "goal": "双方试探，伏笔埋下", "summary": "..."}
              ]
            }

            deleteNumbers 是要删除的原章节编号列表（整数数组）。
            insertChapters 是替换插入的新章节列表，按叙事顺序排列。
            如果只是合并（减少章节数），insertChapters 中填合并后的章节即可。
            """,
        ToolNames = [],
        MaxSteps = 1,
    };
}
