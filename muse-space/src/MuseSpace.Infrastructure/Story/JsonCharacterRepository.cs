using Microsoft.Extensions.Options;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Story;

public sealed class JsonCharacterRepository : JsonRepositoryBase, ICharacterRepository
{
    private readonly string _basePath;

    public JsonCharacterRepository(IOptions<DataOptions> options)
        => _basePath = options.Value.BasePath;

    private string FilePath(Guid projectId) => GetProjectFilePath(_basePath, projectId, "characters.json");

    public Task<List<Character>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => ReadFileAsync<Character>(FilePath(projectId), cancellationToken);

    public async Task<Character?> GetByIdAsync(Guid projectId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var all = await GetByProjectAsync(projectId, cancellationToken);
        return all.FirstOrDefault(c => c.Id == characterId);
    }

    public async Task SaveAsync(Guid projectId, Character character, CancellationToken cancellationToken = default)
    {
        var all = await GetByProjectAsync(projectId, cancellationToken);
        var index = all.FindIndex(c => c.Id == character.Id);
        if (index >= 0) all[index] = character;
        else all.Add(character);
        await WriteFileAsync(FilePath(projectId), all, cancellationToken);
    }

    public async Task DeleteAsync(Guid projectId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var all = await GetByProjectAsync(projectId, cancellationToken);
        all.RemoveAll(c => c.Id == characterId);
        await WriteFileAsync(FilePath(projectId), all, cancellationToken);
    }
}
