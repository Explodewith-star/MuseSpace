namespace MuseSpace.Contracts.Export;

/// <summary>
/// 章节导出格式。
/// </summary>
public enum ChapterExportFormat
{
    /// <summary>Markdown，扩展 .md。</summary>
    Markdown = 0,

    /// <summary>纯文本，扩展 .txt。</summary>
    PlainText = 1,
}
