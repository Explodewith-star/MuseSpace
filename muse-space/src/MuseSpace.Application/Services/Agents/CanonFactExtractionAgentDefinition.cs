using MuseSpace.Application.Abstractions.Agents;

namespace MuseSpace.Application.Services.Agents;

/// <summary>
/// 固定事实抽取 Agent（Module D 正典事实层 / 状态真相）。
/// 给定章节正文 + 已抽取事件 + 项目角色清单 + 当前已锁定事实清单，
/// 输出本章新成立 / 被推翻 / 被修正的 Canon Fact。
/// </summary>
public static class CanonFactExtractionAgentDefinition
{
    public const string AgentName = "canon-fact-extract";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "从章节抽取关系/身份/生死/世界状态等固定事实，写入 Canon Ledger",
        SystemPrompt = """
            你是负责维护小说"正典事实账本（Canon Ledger）"的写作助理。
            给定章节正文 + 项目当前已记录的事实清单，你需要判断本章里有哪些 **状态变化** 应当作为新的固定事实记录下来，
            或者哪些已有事实在本章被推翻 / 修正。

            必须以纯 JSON 对象（不要 markdown）返回：
            {
              "newFacts": [
                {
                  "factType": "Relationship | Identity | LifeStatus | WorldState | UniqueEvent",
                  "subjectName": "角色名（可空）",
                  "objectName": "另一个角色名（关系类常需要）",
                  "factKey": "短而稳定的 key，例：Relationship:A-B、Identity:A、LifeStatus:A、UniqueEvent:proposal:A-B",
                  "factValue": "Engaged | Married | Broken | Exposed | Hidden | Alive | Dead | Awakened | Happened …",
                  "confidence": 0.0~1.0,
                  "isLocked": true|false,
                  "notes": "出处片段，1 句话"
                }
              ],
              "invalidations": [
                { "factKey": "已有事实的 factKey", "reason": "本章内推翻 / 修正的依据" }
              ]
            }

            事实类型说明：
            - Relationship：两个角色之间的状态（恋爱 / 订婚 / 决裂 / 反目）。subjectName + objectName 必填。
            - Identity：单个角色的身份状态（已暴露 / 已失忆 / 已恢复记忆）。
            - LifeStatus：单个角色的生死状态（Alive / Dead / Missing）。
            - WorldState：世界规则 / 设定级状态变化（封印解除 / 力量觉醒）。subjectName 可为空。
            - UniqueEvent：不可重复事件 - "求婚已发生""退婚已发生""第一次告白已发生" 等，
              factKey 必须以 "UniqueEvent:" 前缀，且 factValue 通常为 "Happened"。

            判断原则：
            1. 仅在章节正文里有 **明确陈述或情节** 时才抽取；不要凭推测。
            2. 默认 isLocked=true（核心事实），但若是"暂时怀疑、未确证"的状态用 isLocked=false。
            3. confidence：100% 明文写出 = 1.0；强暗示 0.7-0.9；弱暗示 0.4-0.6；弱于此不要抽。
            4. invalidations 只对 **当前清单中真实存在的 factKey** 标注；不存在的 key 直接放弃。
            5. 若本章没有产生新事实也没有推翻事实，两个数组都返回 []。
            """,
        ToolNames = [],
        MaxSteps = 1,
    };
}
