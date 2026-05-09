namespace MuseSpace.Application.Abstractions.Skills;

public class SkillResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public string SkillName { get; init; } = string.Empty;
    public string? PromptVersion { get; init; }
    public long DurationMs { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }

    /// <summary>
    /// 实际发送给 LLM 的完整 Prompt（system + user 拼接）。
    /// 仅用于排查"草稿与计划不匹配"等问题，注意可能很长。
    /// </summary>
    public string? RenderedPrompt { get; init; }
}
