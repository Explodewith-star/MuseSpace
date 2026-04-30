using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IChapterRepository
{
    Task<List<Chapter>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Chapter?> GetByIdAsync(Guid projectId, Guid chapterId, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid projectId, Chapter chapter, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid projectId, Guid chapterId, CancellationToken cancellationToken = default);
    /// <summary>批量删除指定 ID 的章节（级联删除关联 Scene）。</summary>
    Task<int> BatchDeleteAsync(Guid projectId, IEnumerable<Guid> chapterIds, CancellationToken cancellationToken = default);
    /// <summary>删除由指定大纲建议导入的所有章节。</summary>
    Task<int> DeleteBySourceSuggestionIdAsync(Guid suggestionId, CancellationToken cancellationToken = default);
    /// <summary>按 <paramref name="orderedChapterIds"/> 顺序重排章节 Number，从 <paramref name="startNumber"/> 起。返回实际更新数量。</summary>
    Task<int> BatchReorderAsync(Guid projectId, IReadOnlyList<Guid> orderedChapterIds, int startNumber, CancellationToken cancellationToken = default);
}
