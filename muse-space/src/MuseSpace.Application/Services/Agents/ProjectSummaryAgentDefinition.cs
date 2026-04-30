using MuseSpace.Application.Abstractions.Agents;

namespace MuseSpace.Application.Services.Agents;

/// <summary>
/// 项目摘要 Agent 定义。
/// 给定项目当前的角色 / 世界观 / 大纲 / 已完成草稿等上下文，输出一份"创作进展摘要+下一步建议"。
/// 结果作为一条 ProjectSummary 类目建议写入建议中心。
/// </summary>
public static class ProjectSummaryAgentDefinition
{
    public const string AgentName = "project-summary";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "汇总项目当前进展（角色/世界观/大纲/草稿覆盖度）并给出下一步建议",
        SystemPrompt = """
            你是负责给作者复盘进度的写作助理。根据用户提供的项目快照，写一份简洁的进展摘要和接下来 2~3 条最有价值的行动建议。

            输出要求：
            1. 必须是纯 JSON 对象，不要 markdown 代码块。
            2. 字段：
               - headline (string): 一句话总结当前阶段，如"已完成 12/40 章草稿，整体节奏偏紧"
               - highlights (string[]): 3 条以内的客观进展亮点
               - risks (string[]): 3 条以内潜在风险（节奏/世界观漏洞/角色弧线断裂等）
               - nextActions (string[]): 2~3 条最优先的下一步建议，每条不超过 40 字
            3. 不需要过度赞美，重在指出真实问题。
            """,
        ToolNames = [],
        MaxSteps = 1,
    };
}
