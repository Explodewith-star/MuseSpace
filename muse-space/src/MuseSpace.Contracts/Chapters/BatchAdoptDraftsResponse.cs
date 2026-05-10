namespace MuseSpace.Contracts.Chapters;

/// <summary>
/// 批量采用草稿为定稿的结果。
/// </summary>
public sealed class BatchAdoptDraftsResponse
{
    public int RequestedCount { get; set; }
    public int AdoptedCount { get; set; }
    public int SkippedNoDraftCount { get; set; }
    public int SkippedExistingFinalCount { get; set; }
}
