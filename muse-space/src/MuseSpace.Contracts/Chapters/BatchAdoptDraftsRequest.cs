namespace MuseSpace.Contracts.Chapters;

/// <summary>
/// 批量将章节草稿采用为定稿。
/// </summary>
public sealed class BatchAdoptDraftsRequest
{
    /// <summary>限定故事大纲。为空时处理项目内全部章节。</summary>
    public Guid? StoryOutlineId { get; set; }

    /// <summary>是否覆盖已有定稿。默认 false：已有 FinalText 的章节会跳过。</summary>
    public bool OverrideExisting { get; set; }
}
