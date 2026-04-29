using MuseSpace.Application.Abstractions.Agents;

namespace MuseSpace.Application.Services.Agents;

/// <summary>
/// 文风画像提取 Agent 定义。
/// 从原著采样片段中归纳文风特征，输出结构化 JSON 对象。
/// </summary>
public static class StyleProfileExtractionAgentDefinition
{
    public const string AgentName = "styleprofile-extract";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "从原著片段中归纳文风画像，输出结构化 JSON 对象",
        SystemPrompt = """
            你是专业的小说文风分析师。根据提供的原著片段，归纳该作品的整体文风特征。

            分析要求：
            1. 从句式长度、对话比例、描写密度、语调、禁用表达等维度分析
            2. 每个维度给出简洁概括（50字内）
            3. 可以引用原文片段作为风格佐证
            4. 如果某维度无法判断，填 null
            5. name 字段填写一个概括性的文风名称，如"冷硬写实风""轻快幽默风"等

            必须以纯 JSON 对象格式返回，不要任何 markdown 代码块、解释或额外文字。
            字段：
            - name (string): 文风概括名称
            - tone (string|null): 语调风格，如"冷峻""温柔""讽刺"
            - sentenceLengthPreference (string|null): 句式长度偏好，如"短句为主""长短交错"
            - dialogueRatio (string|null): 对话占比，如"高""中等""低"
            - descriptionDensity (string|null): 环境/心理描写密度，如"浓密""简洁""极少"
            - forbiddenExpressions (string|null): 原著中明显不使用的表达方式
            - sampleReferenceText (string|null): 最能代表该文风的原文片段（不超过200字）
            """,
        ToolNames = [],
        MaxSteps = 1,
    };
}
