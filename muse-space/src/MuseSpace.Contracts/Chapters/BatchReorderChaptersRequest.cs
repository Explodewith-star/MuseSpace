namespace MuseSpace.Contracts.Chapters;

/// <summary>
/// 批量重排章节编号。<see cref="ChapterIds"/> 顺序即为目标编号（第 1 项 → Number=1）。
/// 未在列表中的章节不受影响。
/// </summary>
public sealed class BatchReorderChaptersRequest
{
    /// <summary>目标故事大纲。用于确认本次重排只作用于同一条大纲。</summary>
    public Guid? StoryOutlineId { get; set; }

    public List<Guid> ChapterIds { get; set; } = [];

    /// <summary>
    /// 起始编号，默认 1。允许从其他数字开始（极少用，但保留扩展点）。
    /// </summary>
    public int StartNumber { get; set; } = 1;
}
