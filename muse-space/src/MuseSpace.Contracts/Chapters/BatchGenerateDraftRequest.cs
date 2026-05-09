namespace MuseSpace.Contracts.Chapters;

/// <summary>批量生成章节草稿的请求体。</summary>
public sealed class BatchGenerateDraftRequest
{
    /// <summary>目标故事大纲。为空时使用默认大纲。</summary>
    public Guid? StoryOutlineId { get; set; }

    public int FromNumber { get; set; }
    public int ToNumber { get; set; }

    /// <summary>是否跳过已有草稿的章节。默认 false（允许覆盖原草稿）。</summary>
    public bool SkipChaptersWithDraft { get; set; }

    /// <summary>是否在生成草稿前自动调用写作计划填充。默认 true。</summary>
    public bool AutoFillPlan { get; set; } = true;
}
