namespace MuseSpace.Application.Services.Agents;

using MuseSpace.Application.Abstractions.Agents;

/// <summary>
/// 章节计划生成 Agent。
/// 根据章节的 title/goal/summary 以及项目角色/世界观，自动产出结构化的章节计划（冲突、情感曲线、关键角色、必中要点）。
/// </summary>
public static class ChapterPlanGenerationAgentDefinition
{
    public const string AgentName = "chapter-plan";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "根据章节大纲条目自动产出本章节的写作计划（冲突/情感曲线/关键角色/必中要点）",
        SystemPrompt = """
            你是一名小说章节策划师。你的任务是把一个章节的大纲条目（标题/目标/摘要），结合项目可用角色与世界观，产出一份**结构化章节计划**，供后续草稿生成调用。

            产出原则：
            1. conflict：本章核心冲突，1~2 句话点出对立面与张力来源
            2. emotionCurve：本章情感曲线，用 3~5 个节拍表示，用 → 连接，例如「平静→好奇→惊愕→愤怒→决断」
            3. keyCharacterIds：从「可用角色」列表中挑选本章重点出场角色的 id（仅返回 id 字符串数组）；不要发明角色
            4. mustIncludePoints：本章必须命中的剧情/信息要点 3~6 条；要具体可执行，避免空话
            5. 必须严格围绕给定章节目标和摘要展开，不要扩写新剧情

            必须以纯 JSON 对象格式返回，不要任何 markdown 代码块、解释或额外文字。
            返回结构：
            {
              "conflict": "...",
              "emotionCurve": "...",
              "keyCharacterIds": ["<guid>", "<guid>"],
              "mustIncludePoints": ["...", "...", "..."]
            }

            如果可用角色列表为空，keyCharacterIds 返回空数组。
            """,
        ToolNames = [],
        MaxSteps = 1,
    };
}
