namespace MuseSpace.Application.Abstractions.Prompt;

/// <summary>
/// Prompt 模板的数据结构，对应 prompts/{category}/{name}.md 文件中的 4 个 Section。
/// 由 IPromptTemplateProvider 从文件系统加载，由 IPromptTemplateRenderer 渲染变量后传给模型。
/// </summary>
public class PromptTemplate
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    /// <summary>对应 ## system —— 角色设定与全局行为规则</summary>
    public string System { get; init; } = string.Empty;
    /// <summary>对应 ## instruction —— 核心任务指令，包含 {{变量}} 占位符</summary>
    public string Instruction { get; init; } = string.Empty;
    /// <summary>对应 ## context —— 补充背景信息（可选）</summary>
    public string Context { get; init; } = string.Empty;
    /// <summary>对应 ## output_format —— 输出格式要求，通常为 JSON 结构说明</summary>
    public string OutputFormat { get; init; } = string.Empty;
}
