namespace MuseSpace.Contracts.Suggestions;

/// <summary>用户审核编辑后的大纲导入请求。</summary>
public sealed class ImportOutlineRequest
{
    /// <summary>要导入的章节列表（用户可能已编辑、删除或调整编号）。</summary>
    public List<ImportOutlineChapter> Chapters { get; init; } = [];
}

/// <summary>单章信息。</summary>
public sealed class ImportOutlineChapter
{
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Goal { get; init; }
    public string? Summary { get; init; }
}
