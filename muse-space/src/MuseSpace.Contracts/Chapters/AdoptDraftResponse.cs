namespace MuseSpace.Contracts.Chapters;

/// <summary>
/// AdoptDraft 接口的结果。
/// </summary>
public sealed class AdoptDraftResponse
{
    /// <summary>是否实际写入了定稿。false 表示因冲突拒绝（前端应弹二次确认）。</summary>
    public bool Adopted { get; set; }

    /// <summary>采用后定稿的字符长度（Adopted=true 时返回）。</summary>
    public int FinalLength { get; set; }

    /// <summary>原定稿的字符长度（用于冲突场景下的二次确认提示）。</summary>
    public int PreviousFinalLength { get; set; }

    /// <summary>草稿的字符长度。</summary>
    public int DraftLength { get; set; }
}

/// <summary>
/// AdoptDraft 失败原因枚举。
/// </summary>
public static class AdoptDraftFailureReasons
{
    public const string DraftEmpty = "DRAFT_EMPTY";
    public const string ExistingFinalConflict = "EXISTING_FINAL_CONFLICT";
}
