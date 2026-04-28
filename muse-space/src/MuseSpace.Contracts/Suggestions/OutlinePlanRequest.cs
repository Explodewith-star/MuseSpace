namespace MuseSpace.Contracts.Suggestions;

/// <summary>大纲规划请求。</summary>
public sealed class OutlinePlanRequest
{
    /// <summary>故事目标描述。</summary>
    public string Goal { get; init; } = string.Empty;

    /// <summary>期望生成的章节数量（默认 10）。</summary>
    public int ChapterCount { get; init; } = 10;

    /// <summary>规划模式："new" = 全新规划，"continue" = 续写扩展。</summary>
    public string Mode { get; init; } = "new";
}
