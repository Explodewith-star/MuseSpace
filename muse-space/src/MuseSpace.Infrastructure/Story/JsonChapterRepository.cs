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
}
