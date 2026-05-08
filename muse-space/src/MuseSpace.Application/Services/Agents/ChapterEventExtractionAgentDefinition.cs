using MuseSpace.Application.Abstractions.Agents;

namespace MuseSpace.Application.Services.Agents;

/// <summary>
/// 章节事件抽取 Agent（Module D 正典事实层 / 时间线）。
/// 给定章节正文 + 项目角色清单，抽取本章发生的可结构化事件（求婚、暴露、决斗、重伤、死亡 等）。
/// </summary>
public static class ChapterEventExtractionAgentDefinition
{
    public const string AgentName = "chapter-event-extract";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "从章节正文抽取结构化事件，用于时间线 / 重复事件守护",
        SystemPrompt = """
            你是负责从小说章节正文中提取结构化事件的写作助理。
            你只负责抽取 **本章节内确实发生** 的、对故事有推进意义的关键事件。

            必须以纯 JSON 对象（不要 markdown，不要解释文字）返回，字段：
            - events (array): 本章关键事件，每项：
              - eventType (string): 事件类型；优先使用以下枚举之一，若都不合适才自创短语：
                Proposal(求婚) / RejectMarriage(退婚) / Engagement(订婚) / Marriage(成婚) / Breakup(决裂)
                / IdentityReveal(身份暴露) / MemoryLoss(失忆) / MemoryRecover(恢复记忆)
                / Death(死亡) / SeriousInjury(重伤) / Awakening(觉醒)
                / Battle(战斗) / Reconcile(和解) / Betrayal(背叛) / Pact(立约)
                / Custom(其它)
              - eventText (string): 1-2 句客观陈述，避免主观评论
              - actorNames (array<string>|null): 动作发起者角色名（按正文里的称谓写）
              - targetNames (array<string>|null): 动作承受者 / 目标角色名
              - location (string|null): 地点
              - timePoint (string|null): 时间点（如"中元节当夜"）
              - importance (High|Medium|Low): 默认 Medium，明显主线才标 High
              - isIrreversible (boolean): 是否为不可重复事件（求婚 / 退婚 / 第一次告白 / 决斗 / 死亡 等通常 true；
                日常对话、推理、观察一般 false）
              - order (int): 事件在本章中的发生顺序，从 1 开始

            判断原则：
            1. **只抽本章正文里明确发生的事件**。回忆 / 闪回 / 角色梦境 / 计划中未发生 不要抽。
            2. 若本章没有任何关键事件，返回 events: []。
            3. 不要抽"角色心情""场景描述"这类非事件。
            4. 同一事件不要重复抽取。
            5. isIrreversible 一旦标 true，意味着后续章节里同一组人不应再发生同 eventType（如不可第二次求婚）。
            """,
        ToolNames = [],
        MaxSteps = 1,
    };
}
