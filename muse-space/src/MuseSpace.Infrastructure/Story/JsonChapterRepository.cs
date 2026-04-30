using Microsoft.Extensions.Options;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Story;

public sealed class JsonChapterRepository : JsonRepositoryBase, IChapterRepository
{
    private readonly string _basePath;

    public JsonChapterRepository(IOptions<DataOptions> options)
        => _basePath = options.Value.BasePath;

    private string FilePath(Guid projectId) => GetProjectFilePath(_basePath, projectId, "chapters.json");

    public Task<List<Chapter>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => ReadFileAsync<Chapter>(FilePath(projectId), cancellationToken);

    public async Task<Chapter?> GetByIdAsync(Guid projectId, Guid chapterId, CancellationToken cancellationToken = default)
    {
        var all = await GetByProjectAsync(projectId, cancellationToken);
        return all.FirstOrDefault(c => c.Id == chapterId);
    }

    public async Task SaveAsync(Guid projectId, Chapter chapter, CancellationToken cancellationToken = default)
    {
        var all = await GetByProjectAsync(projectId, cancellationToken);
        var index = all.FindIndex(c => c.Id == chapter.Id);
        if (index >= 0) all[index] = chapter;
        else all.Add(chapter);
        await WriteFileAsync(FilePath(projectId), all, cancellationToken);
    }

    public async Task DeleteAsync(Guid projectId, Guid chapterId, CancellationToken cancellationToken = default)
    {
        var all = await GetByProjectAsync(projectId, cancellationToken);
        all.RemoveAll(c => c.Id == chapterId);
        await WriteFileAsync(FilePath(projectId), all, cancellationToken);
    }

    public async Task<int> BatchDeleteAsync(Guid projectId, IEnumerable<Guid> chapterIds, CancellationToken cancellationToken = default)
    {
        var ids = chapterIds.ToHashSet();
        if (ids.Count == 0) return 0;
        var all = await GetByProjectAsync(projectId, cancellationToken);
        var before = all.Count;
        all.RemoveAll(c => ids.Contains(c.Id));
        await WriteFileAsync(FilePath(projectId), all, cancellationToken);
        return before - all.Count;
    }

    public async Task<int> DeleteBySourceSuggestionIdAsync(Guid suggestionId, CancellationToken cancellationToken = default)
    {
        // JSON 仓储无法跨项目查，遍历所有项目目录
        var dir = Path.Combine(_basePath, "projects");
        if (!Directory.Exists(dir)) return 0;
        var total = 0;
        foreach (var projectDir in Directory.GetDirectories(dir))
        {
            if (!Guid.TryParse(Path.GetFileName(projectDir), out var projectId)) continue;
            var path = FilePath(projectId);
            if (!File.Exists(path)) continue;
            var all = await ReadFileAsync<Chapter>(path, cancellationToken);
            var before = all.Count;
            all.RemoveAll(c => c.SourceSuggestionId == suggestionId);
            if (all.Count != before)
            {
                await WriteFileAsync(path, all, cancellationToken);
                total += before - all.Count;
            }
        }
        return total;
    }

    public async Task<int> BatchReorderAsync(
        Guid projectId,
        IReadOnlyList<Guid> orderedChapterIds,
        int startNumber,
        CancellationToken cancellationToken = default)
    {
        if (orderedChapterIds.Count == 0) return 0;
        var all = await GetByProjectAsync(projectId, cancellationToken);
        if (all.Count == 0) return 0;
        var map = all.ToDictionary(c => c.Id);
        var updated = 0;
        for (var i = 0; i < orderedChapterIds.Count; i++)
        {
            if (!map.TryGetValue(orderedChapterIds[i], out var chapter)) continue;
            var target = startNumber + i;
            if (chapter.Number != target)
            {
                chapter.Number = target;
                updated++;
            }
        }
        if (updated > 0)
            await WriteFileAsync(FilePath(projectId), all, cancellationToken);
        return updated;
    }
}
