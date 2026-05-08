namespace MuseSpace.Domain.Enums;

/// <summary>
/// 章节草稿生成模式（Module E 续写/外传模式）。
/// 控制 StoryContextBuilder 如何组装上下文并在 Prompt 中声明创作约束。
/// </summary>
public enum GenerationMode
{
    /// <summary>纯原创模式：按当前项目大纲生成，不依赖外部原著。</summary>
    Original = 0,

    /// <summary>原著续写：接在导入原著的已知结尾后继续写。</summary>
    ContinueFromOriginal = 1,

    /// <summary>支线番外：从原著某人物线、章节范围或事件点生成支线。</summary>
    SideStoryFromOriginal = 2,

    /// <summary>扩写/改写：对已有章节或参考片段做扩写 / 改写。</summary>
    ExpandOrRewrite = 3,
}
