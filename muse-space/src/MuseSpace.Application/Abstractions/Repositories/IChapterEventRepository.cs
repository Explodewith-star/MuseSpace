using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IChapterEventRepository
{
    Task<List<ChapterEvent>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<List<ChapterEvent>> GetByChapterAsync(Guid projectId, Guid chapterId, CancellationToken ct = default);
    Task<List<ChapterEvent>> GetRecentAsync(Guid projectId, int chapterCount, CancellationToken ct = default);
    Task<List<ChapterEvent>> GetIrreversibleAsync(Guid projectId, CancellationToken ct = default);
    Task<ChapterEvent?> GetByIdAsync(Guid projectId, Guid id, CancellationToken ct = default);
    Task<ChapterEvent> AddAsync(ChapterEvent ev, CancellationToken ct = default);
    Task UpdateAsync(ChapterEvent ev, CancellationToken ct = default);
    Task DeleteAsync(Guid projectId, Guid id, CancellationToken ct = default);

    /// <summary>批量替换某章事件（PUT 语义）。</summary>
    Task ReplaceForChapterAsync(Guid projectId, Guid chapterId, IReadOnlyList<ChapterEvent> events, CancellationToken ct = default);
}
