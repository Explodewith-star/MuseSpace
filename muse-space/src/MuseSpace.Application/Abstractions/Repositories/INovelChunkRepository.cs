using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface INovelChunkRepository
{
    Task<List<NovelChunk>> GetByNovelAsync(Guid novelId, CancellationToken cancellationToken = default);

    /// <summary>获取尚未生成 Embedding 的切片，用于 Embedding 批处理</summary>
    Task<List<NovelChunk>> GetUnembeddedAsync(Guid novelId, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<NovelChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>标记切片已完成 Embedding</summary>
    Task MarkEmbeddedAsync(Guid chunkId, CancellationToken cancellationToken = default);

    /// <summary>批量标记切片已完成 Embedding</summary>
    Task MarkEmbeddedBatchAsync(IEnumerable<Guid> chunkIds, CancellationToken cancellationToken = default);

    Task DeleteByNovelAsync(Guid novelId, CancellationToken cancellationToken = default);
}
