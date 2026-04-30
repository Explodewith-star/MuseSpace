namespace MuseSpace.Contracts.Chapters;

/// <summary>
/// 一键将草稿采用为定稿的请求体。
/// </summary>
public sealed class AdoptDraftRequest
{
    /// <summary>
    /// 当章节已有 FinalText 时是否覆盖。默认 false：未指定且定稿非空时返回 409。
    /// </summary>
    public bool OverrideExisting { get; set; }
}
