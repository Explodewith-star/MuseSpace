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

    // ── Module D 正典事实层 ─────────────────────────────────────

    /// <summary>最近章节的详细事件时间线（已以“第N章 - 事件描述”格式化）。</summary>
    public List<string> RecentEvents { get; init; } = [];

    /// <summary>当前人物关系与状态快照（未被废弃的 Relationship / Identity / LifeStatus 活跃事实）。</summary>
    public List<string> CharacterStateFacts { get; init; } = [];

    /// <summary>不可重复 / 不可改写事实（已锁定的 UniqueEvent 事实 + 过往不可逆事件）。</summary>
    public List<string> ImmutableFacts { get; init; } = [];

    // ── Module E 续写/外传模式 ────────────────────────────────────────

    /// <summary>当前生成模式格式化为中文描述（用于 Prompt 头部声明）。如果是 Original 则为 null。</summary>
    public string? GenerationModeHeader { get; init; }

    /// <summary>原著结尾片段（续写模式下注入，约最后 3 个 chunk 的文本）。</summary>
    public List<string> NovelEndingSnippets { get; init; } = [];

    /// <summary>支线主题及原著指定章节范围片段（支线模式下注入）。</summary>
    public List<string> BranchContextSnippets { get; init; } = [];

    /// <summary>偏离原著许可程度的文字说明（用于 Prompt 声明边界）。</summary>
    public string? DivergencePolicyNote { get; init; }

    /// <summary>原著大结局摘要（来自 Novel.EndingSummary，优先于 raw chunk 注入）。</summary>
    public string? NovelEndingSummary { get; init; }

    /// <summary>原著文风摘要（来自 Novel.StyleSummary，续写/番外时补充文风约束）。</summary>
    public string? NovelStyleSummary { get; init; }

    /// <summary>
    /// 原著主要角色结尾状态列表（来自 novel_character_snapshots）。
    /// 格式："【角色名】状态描述"，★ 标注不可逆。
    /// </summary>
    public List<string> NovelCharacterEndStates { get; init; } = [];
}
