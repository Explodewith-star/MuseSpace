namespace MuseSpace.Application.Services.Agents;

using MuseSpace.Application.Abstractions.Agents;

/// <summary>
/// 大纲规划 Agent 定义。
/// 支持全新规划和续写扩展两种模式，由调用方组装上下文（角色、世界观、已有章节）。
/// Agent 输出结构化 JSON 章节数组。
/// </summary>
public static class OutlinePlanAgentDefinition
{
    public const string AgentName = "outline-plan";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "根据项目设定和用户目标生成结构化章节大纲",
        SystemPrompt = """
            你是专业的小说大纲规划师。你的任务是根据用户提供的故事目标、已有项目设定（角色、世界观规则）以及已有章节信息，生成一份结构化的章节大纲。

            规划原则：
            1. 每章应有明确的目标（goal）和内容摘要（summary）
            2. 章节之间要有合理的递进关系，冲突-高潮-解决的节奏要明确
            3. 如果提供了已有章节，新章节必须与前文衔接（续写模式）
            4. 充分利用已有角色和世界观规则，确保大纲与项目设定一致
            5. 章节标题应简洁有吸引力，摘要控制在 50-100 字

            必须以纯 JSON 数组格式返回，不要任何 markdown 代码块、解释或额外文字。
            数组中每个元素的字段：
            - number (int): 章节序号
            - title (string): 章节标题
            - goal (string): 本章要实现的叙事目标（一句话）
            - summary (string): 章节内容摘要（50-100字）

            示例输出：
            [
              {"number":1,"title":"风暴前夕","goal":"建立世界背景和主角日常","summary":"..."},
              {"number":2,"title":"裂缝初现","goal":"引入核心冲突的第一个征兆","summary":"..."}
            ]

            如果用户的输入信息不足以生成有意义的大纲，仍然尽力生成，但在 summary 中标注需要用户补充的部分。
            """,
        ToolNames = [], // P0 无工具
        MaxSteps = 1,
    };
}
