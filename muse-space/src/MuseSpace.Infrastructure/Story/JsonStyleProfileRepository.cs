using Microsoft.Extensions.Options;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Story;

public sealed class JsonStyleProfileRepository : JsonRepositoryBase, IStyleProfileRepository
{
    private readonly string _basePath;

    public JsonStyleProfileRepository(IOptions<DataOptions> options)
        => _basePath = options.Value.BasePath;

    private string FilePath(Guid projectId) => GetProjectFilePath(_basePath, projectId, "style-profile.json");

    public Task<StyleProfile?> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => ReadSingleFileAsync<StyleProfile>(FilePath(projectId), cancellationToken);

    public Task SaveAsync(Guid projectId, StyleProfile profile, CancellationToken cancellationToken = default)
        => WriteSingleFileAsync(FilePath(projectId), profile, cancellationToken);

    public Task DeleteAsync(Guid projectId, Guid profileId, CancellationToken cancellationToken = default)
    {
        var path = FilePath(projectId);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }
}
