namespace MuseSpace.Application.Abstractions.Memory;

/// <summary>
/// 原著记忆检索服务。给定场景描述文本，返回最相关的原著切片。
/// </summary>
public interface INovelMemorySearchService
{
    /// <summary>按项目搜索（全部原著）。</summary>
    Task<IReadOnlyList<NovelChunkSearchResult>> SearchAsync(
        Guid projectId,
        string queryText,
        int topK = 5,
        CancellationToken ct = default);

    /// <summary>按指定原著搜索（续写/番外时限定来源）。</summary>
    Task<IReadOnlyList<NovelChunkSearchResult>> SearchByNovelAsync(
        Guid novelId,
        string queryText,
        int topK = 5,
        CancellationToken ct = default);
}
