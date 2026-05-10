using Microsoft.Extensions.Options;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

namespace MuseSpace.Infrastructure.Story;

public sealed class JsonChapterRepository : JsonRepositoryBase, IChapterRepository
{
    private readonly string _basePath;

    public JsonChapterRepository(IOptions<DataOptions> options)
        => _basePath = options.Value.BasePath;

    private string FilePath(Guid projectId) => GetProjectFilePath(_basePath, projectId, "chapters.json");

    public Task<List<Chapter>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => ReadFileAsync<Chapter>(FilePath(projectId), cancellationToken);

    public async Task<List<Chapter>> GetByOutlineAsync(
        Guid projectId,
        Guid outlineId,
        CancellationToken cancellationToken = default)
    {
        var all = await GetByProjectAsync(projectId, cancellationToken);
        return all
            .Where(c => c.StoryOutlineId == outlineId)
            .OrderBy(c => c.Number)
            .ToList();
    }

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

    public async Task<int> DeleteBySourceSuggestionIdAsync(
        Guid suggestionId,
        Guid storyOutlineId,
        CancellationToken cancellationToken = default)
    {
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
            all.RemoveAll(c => c.SourceSuggestionId == suggestionId && c.StoryOutlineId == storyOutlineId);
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
        var first = all.FirstOrDefault(c => orderedChapterIds.Contains(c.Id));
        if (first is null) return 0;
        return await BatchReorderAsync(projectId, first.StoryOutlineId, orderedChapterIds, startNumber, cancellationToken);
    }

    public async Task<int> BatchReorderAsync(
        Guid projectId,
        Guid storyOutlineId,
        IReadOnlyList<Guid> orderedChapterIds,
        int startNumber,
        CancellationToken cancellationToken = default)
    {
        if (orderedChapterIds.Count == 0) return 0;
        var all = await GetByProjectAsync(projectId, cancellationToken);
        if (all.Count == 0) return 0;
        var scoped = all.Where(c => c.StoryOutlineId == storyOutlineId).ToList();
        var map = scoped.ToDictionary(c => c.Id);
        var missing = orderedChapterIds.Any(id => !map.ContainsKey(id));
        if (missing)
            throw new InvalidOperationException("重排章节必须全部属于同一故事大纲");
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

    public async Task<(int RequestedCount, int AdoptedCount, int SkippedNoDraftCount, int SkippedExistingFinalCount)> BatchAdoptDraftAsync(
        Guid projectId,
        IReadOnlyCollection<Guid> chapterIds,
        bool overrideExisting,
        CancellationToken cancellationToken = default)
    {
        if (chapterIds.Count == 0) return (0, 0, 0, 0);

        var ids = chapterIds.ToHashSet();
        var all = await GetByProjectAsync(projectId, cancellationToken);
        var scoped = all.Where(c => ids.Contains(c.Id)).ToList();
        var skippedNoDraft = 0;
        var skippedExistingFinal = 0;
        var adopted = 0;

        foreach (var chapter in scoped)
        {
            if (string.IsNullOrWhiteSpace(chapter.DraftText))
            {
                skippedNoDraft++;
                continue;
            }

            if (!overrideExisting && !string.IsNullOrWhiteSpace(chapter.FinalText))
            {
                skippedExistingFinal++;
                continue;
            }

            chapter.FinalText = chapter.DraftText;
            if (chapter.Status < ChapterStatus.Finalized)
            {
                chapter.Status = ChapterStatus.Finalized;
            }
            adopted++;
        }

        if (adopted > 0)
        {
            await WriteFileAsync(FilePath(projectId), all, cancellationToken);
        }

        return (scoped.Count, adopted, skippedNoDraft, skippedExistingFinal);
    }
}
