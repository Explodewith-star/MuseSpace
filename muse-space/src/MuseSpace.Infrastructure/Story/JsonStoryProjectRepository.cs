using Microsoft.Extensions.Options;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Story;

public sealed class JsonStoryProjectRepository : JsonRepositoryBase, IStoryProjectRepository
{
    private readonly string _basePath;

    public JsonStoryProjectRepository(IOptions<DataOptions> options)
        => _basePath = options.Value.BasePath;

    private string FilePath(Guid id) => GetProjectFilePath(_basePath, id, "project.json");

    public async Task<StoryProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await ReadSingleFileAsync<StoryProject>(FilePath(id), cancellationToken);

    public async Task<List<StoryProject>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var projectsDir = Path.Combine(_basePath, "projects");
        if (!Directory.Exists(projectsDir)) return [];

        var results = new List<StoryProject>();
        foreach (var dir in Directory.GetDirectories(projectsDir))
        {
            var filePath = Path.Combine(dir, "project.json");
            var project = await ReadSingleFileAsync<StoryProject>(filePath, cancellationToken);
            if (project is not null) results.Add(project);
        }
        return results;
    }

    public async Task SaveAsync(StoryProject project, CancellationToken cancellationToken = default)
        => await WriteSingleFileAsync(FilePath(project.Id), project, cancellationToken);

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dir = GetProjectDir(_basePath, id);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        await Task.CompletedTask;
    }
}
