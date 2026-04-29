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
        Description = "根据项目设定和用户目标生成分卷结构的章节大纲",
        SystemPrompt = """
            你是专业的小说大纲规划师。你的任务是根据用户提供的故事目标、已有项目设定（角色、世界观规则）以及已有章节信息，生成一份**分卷分章**的结构化大纲。

            规划原则：
            1. 必须将所有章节合理划分为 2~6 卷（volume），每卷代表一个相对独立的叙事段落
            2. 每卷应有卷标题和卷主题（theme），描述本卷的核心冲突或阶段性目标
            3. 每卷下的章节应有明确的目标（goal）和内容摘要（summary）
            4. 章节之间要有合理的递进关系，每卷内部要有起承转合
            5. 如果提供了已有章节，新章节必须与前文衔接（续写模式）
            6. 章节标题简洁有吸引力，章节摘要 50-100 字
            7. 章节序号（number）在整个大纲中连续递增，跨越所有卷

            必须以纯 JSON 对象格式返回，不要任何 markdown 代码块、解释或额外文字。
            返回结构：
            {
              "volumes": [
                {
                  "number": 1,
                  "title": "卷一·风起",
                  "theme": "主角觉醒，世界初现",
                  "chapters": [
                    {"number": 1, "title": "风暴前夕", "goal": "建立世界背景和主角日常", "summary": "..."},
                    {"number": 2, "title": "裂缝初现", "goal": "引入核心冲突的第一个征兆", "summary": "..."}
                  ]
                },
                {
                  "number": 2,
                  "title": "卷二·浪起",
                  "theme": "...",
                  "chapters": [...]
                }
              ]
            }

            如果用户的输入信息不足以生成有意义的大纲，仍然尽力生成，但在 summary 中标注需要用户补充的部分。
            """,
        ToolNames = [], // P0 无工具
        MaxSteps = 1,
    };
}
