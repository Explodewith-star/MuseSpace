using MuseSpace.Application.Abstractions.Agents;

namespace MuseSpace.Application.Services.Agents;

/// <summary>
/// 伏笔追踪 Agent 定义。
/// 给定项目当前 PlotThread 列表 + 新生成草稿（或某段范围草稿），分析：
/// 1. 是否新埋伏了未记录的线索（new threads）；
/// 2. 是否已经回收 / 推进 / 遗忘 已存在的线索（updates）。
/// </summary>
public static class PlotThreadTrackingAgentDefinition
{
    public const string AgentName = "plot-thread-tracking";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "分析草稿对伏笔/故事线索的埋设与回收，输出待写入的更新动作",
        SystemPrompt = """
            你是负责追踪长篇小说伏笔的写作助理。给定项目当前的伏笔/线索清单与新生成的章节草稿，
            判断草稿对已有伏笔的影响并发现新埋伏。

            必须以纯 JSON 对象（不要 markdown）返回，字段：
            - newThreads (array): 草稿中新埋设但当前清单尚未记录的线索；每项 { title, description, importance(High|Medium|Low) }
            - updates (array): 对已有线索的状态更新；每项 { id (现有 PlotThread.Id), newStatus(Introduced|Active|PaidOff|Abandoned), reason }
            - notes (string|null): 整体观察（如"本章节集中收回了 2 条主线伏笔"）

            判断原则：
            1. 仅在草稿确实有相关情节时才更新已有线索状态；不要凭空猜测。
            2. 若草稿对某条已有线索没有任何提及，不要列入 updates。
            3. newStatus=PaidOff 必须有明确的回收情节支撑。
            4. 重要度判断保守：默认 Medium，只有明显是核心伏笔才标 High。
            """,
        ToolNames = [],
        MaxSteps = 1,
    };
}
