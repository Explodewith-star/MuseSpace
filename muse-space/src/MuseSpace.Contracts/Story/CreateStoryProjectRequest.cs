namespace MuseSpace.Contracts.Story;

public sealed class CreateStoryProjectRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Genre { get; init; }
    public string? NarrativePerspective { get; init; }
    /// <summary>项目大类：原创主线 / 原著续写 / 直线番外 / 扩写改写。</summary>
    public string? OutlineType { get; init; }
}
