namespace MuseSpace.Application.Abstractions.Memory;

/// <summary>
/// 原著记忆检索服务。给定场景描述文本，返回最相关的原著切片。
/// </summary>
public interface INovelMemorySearchService
{
    Task<IReadOnlyList<NovelChunkSearchResult>> SearchAsync(
        Guid projectId,
        string queryText,
        int topK = 5,
        CancellationToken ct = default);
}
