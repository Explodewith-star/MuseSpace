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

    public async Task<List<Character>> GetByOutlineAsync(Guid outlineId, CancellationToken cancellationToken = default)
    {
        // JSON 存储无法高效按 outlineId 查询，遍历所有项目目录（仅用于本地开发兜底）
        var all = new List<Character>();
        var projectsDir = Path.Combine(_basePath, "projects");
        if (!Directory.Exists(projectsDir)) return all;
        foreach (var dir in Directory.GetDirectories(projectsDir))
        {
            var file = Path.Combine(dir, "characters.json");
            if (!File.Exists(file)) continue;
            var projectId = Guid.Parse(Path.GetFileName(dir));
            var chars = await ReadFileAsync<Character>(FilePath(projectId), cancellationToken);
            all.AddRange(chars.Where(c => c.StoryOutlineId == outlineId));
        }
        return all;
    }

    public async Task<List<Character>> GetPoolByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var all = await GetByProjectAsync(projectId, cancellationToken);
        return all.Where(c => c.StoryOutlineId == null).ToList();
    }

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

    public async Task SaveManyAsync(Guid projectId, IEnumerable<Character> characters, CancellationToken cancellationToken = default)
    {
        var all = await GetByProjectAsync(projectId, cancellationToken);
        foreach (var character in characters)
        {
            character.StoryProjectId = projectId;
            all.Add(character);
        }
        await WriteFileAsync(FilePath(projectId), all, cancellationToken);
    }
}
