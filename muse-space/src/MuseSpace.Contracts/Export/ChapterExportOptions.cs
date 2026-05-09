namespace MuseSpace.Contracts.Export;

/// <summary>
/// 章节导出选项。
/// </summary>
public sealed class ChapterExportOptions
{
    /// <summary>目标故事大纲。为空时导出项目内全部大纲的章节。</summary>
    public Guid? StoryOutlineId { get; set; }

    /// <summary>导出格式。</summary>
    public ChapterExportFormat Format { get; set; } = ChapterExportFormat.Markdown;

    /// <summary>起始章节号（含），null 表示不限。</summary>
    public int? FromNumber { get; set; }

    /// <summary>结束章节号（含），null 表示不限。</summary>
    public int? ToNumber { get; set; }

    /// <summary>仅导出已定稿（Status = Finalized 且 FinalText 非空）。默认 true。</summary>
    public bool OnlyFinal { get; set; } = true;

    /// <summary>
    /// 当 OnlyFinal=false 且某章 FinalText 为空但 DraftText 非空时，
    /// 是否使用草稿作为该章正文（带 "[草稿]" 标识）。默认 false。
    /// </summary>
    public bool IncludeDraftFallback { get; set; }
}
