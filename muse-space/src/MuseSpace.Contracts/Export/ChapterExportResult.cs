namespace MuseSpace.Contracts.Export;

/// <summary>
/// 章节导出结果（用于 Controller 返回 FileContentResult）。
/// </summary>
public sealed class ChapterExportResult
{
    /// <summary>文件名，例：《我的作品》_第1-10章_20260430-1430.md</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>MIME 类型，md 用 text/markdown，txt 用 text/plain。</summary>
    public string ContentType { get; init; } = "text/plain";

    /// <summary>文件内容（UTF-8 + BOM）。</summary>
    public byte[] Content { get; init; } = [];

    /// <summary>本次导出包含的章节数（用于响应头记录或调试）。</summary>
    public int ChapterCount { get; init; }
}
