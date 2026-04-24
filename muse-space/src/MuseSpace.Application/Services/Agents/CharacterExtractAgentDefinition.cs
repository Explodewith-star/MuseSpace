using MuseSpace.Application.Abstractions.Agents;

namespace MuseSpace.Application.Services.Agents;

/// <summary>
/// 角色卡提取 Agent 定义。
/// 从原著片段中识别并提取角色信息，输出结构化 JSON。
///
/// P0 阶段：无工具，单次 LLM 调用（等同于原有 CharactersController.ExtractFromNovel 的 prompt 调用）。
/// P1 阶段可扩展：增加 GetExistingCharactersTool（去重）、SearchNovelChunkTool（补充上下文）等。
/// </summary>
public static class CharacterExtractAgentDefinition
{
    public const string AgentName = "character-extract";

    public static AgentDefinition Create() => new()
    {
        Name = AgentName,
        Description = "从原著片段中提取角色信息，输出结构化 JSON",
        SystemPrompt = """
            你是专业的小说角色分析师。根据提供的原著片段，分析并提取指定角色的信息。
            必须以纯 JSON 格式返回，不要任何 markdown 代码块、解释或额外文字。
            JSON 字段：
            - name (string): 角色全名
            - age (number|null): 年龄，不确定填 null
            - role (string|null): 身份定位，如"主角/侦探/嫌疑人"
            - personalitySummary (string|null): 性格概述，100字内
            - motivation (string|null): 核心动机或目标
            - speakingStyle (string|null): 说话方式特点
            - forbiddenBehaviors (string|null): 该角色绝不会做的事
            - currentState (string|null): 故事中的当前状态
            """,
        ToolNames = [], // P0: 无工具
        MaxSteps = 1,
    };
}
