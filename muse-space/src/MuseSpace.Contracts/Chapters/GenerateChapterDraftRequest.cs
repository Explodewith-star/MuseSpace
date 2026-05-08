namespace MuseSpace.Contracts.Chapters;

public sealed class GenerateChapterDraftRequest
{
    // ── 局部参考（Module B）──────────────────────────────────────────
    public string? ReferenceText { get; init; }
    public string? ReferenceFocus { get; init; }
    public string? ReferenceStrength { get; init; }

    // ── 续写/外传模式（Module E）────────────────────────────────────

    /// <summary>
    /// 生成模式：Original(0) / ContinueFromOriginal(1) / SideStoryFromOriginal(2) / ExpandOrRewrite(3)。
    /// 默认 Original（纯原创）。
    /// </summary>
    public string? GenerationMode { get; init; }

    /// <summary>指定关联原著 ID（续写 / 支线模式必填）。</summary>
    public Guid? SourceNovelId { get; init; }

    /// <summary>续写模式：从原著第几章之后继续（不填则取最后一章）。</summary>
    public int? ContinuationStartChapterNumber { get; init; }

    /// <summary>支线模式：原著范围起始 chunk 索引。</summary>
    public int? OriginalRangeStart { get; init; }

    /// <summary>支线模式：原著范围结束 chunk 索引（不含）。</summary>
    public int? OriginalRangeEnd { get; init; }

    /// <summary>支线相关角色 ID（多选）。</summary>
    public List<Guid>? RelatedCharacterIds { get; init; }

    /// <summary>支线番外主题，如"围绕女主角第三章相遇场景写番外"。</summary>
    public string? BranchTopic { get; init; }

    /// <summary>
    /// 偏离原著许可：StrictCanon / SoftCanon / AlternateTimeline。
    /// 仅在续写 / 支线模式下生效。默认 SoftCanon。
    /// </summary>
    public string? DivergencePolicy { get; init; }
}