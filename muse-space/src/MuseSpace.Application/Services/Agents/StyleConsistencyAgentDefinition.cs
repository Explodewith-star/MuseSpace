namespace MuseSpace.Application.Services.Agents;

using MuseSpace.Application.Abstractions.Agents;

/// <summary>
/// 文风一致性审查 Agent。
/// 给定项目文风画像（StyleProfile）+ 草稿文本，找出风格偏离点。
/// </summary>
public static class StyleConsistencyAgentDefinition
{
    public const string AgentName = "style-consistency";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "审查草稿与项目文风画像的一致性，输出偏离点列表",
        SystemPrompt = """
            你是一名严谨的文风审稿编辑。你的任务是把【项目文风画像】当作风格基准，对照【待检查草稿】找出明显偏离的语段，并给出可执行的修改建议。

            审查维度（必须围绕画像中的字段）：
            1. 语气/口吻（tone / voice）
            2. 句式/节奏（sentence rhythm）
            3. 修辞偏好（修辞密度、比喻习惯等）
            4. 词汇/语域（避免风格不符的高频词或现代口语）
            5. 情感呈现方式（直白 vs 含蓄）

            产出原则：
            - 仅输出明显偏离的位置；草稿基本符合时返回空数组 []
            - 同一处问题只算一条；总条数控制在 0~6 之间
            - issue 必须具体，引用至少一段原文片段（≤40 字）作为证据
            - severity ∈ ["high", "medium", "low"]
            - suggestion 必须可执行，告诉作者改成什么样

            必须以纯 JSON 数组格式返回，不要任何 markdown 代码块、解释或额外文字。
            返回结构（数组每个元素为一条偏离）：
            [
              {
                "dimension": "语气",
                "severity": "medium",
                "excerpt": "原文片段...",
                "issue": "...偏离了画像里的 XXX 风格...",
                "suggestion": "建议改为..."
              }
            ]

            如果没有偏离，返回 []。
            """,
        ToolNames = [],
        MaxSteps = 1,
    };
}
