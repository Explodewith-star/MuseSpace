namespace MuseSpace.Application.Abstractions.Story;

/// <summary>
/// 一次生成请求的上下文快照，由 IStoryContextBuilder 组装。
/// 各字段对应 Prompt 模板中的 {{变量}} 占位符，空值渲染为空字符串。
/// </summary>
public class StoryContext
{
    public string? ProjectSummary { get; init; }
    public List<string> RecentChapterSummaries { get; init; } = [];
    public List<string> InvolvedCharacterCards { get; init; } = [];
    public List<string> WorldRules { get; init; } = [];
    public string? StyleRequirement { get; init; }
    public string SceneGoal { get; init; } = string.Empty;
    public string? Conflict { get; init; }
    public string? EmotionCurve { get; init; }

    /// <summary>原著相关切片，由向量检索注入，供 Prompt 使用</summary>
    public List<string> NovelContextSnippets { get; init; } = [];
}
