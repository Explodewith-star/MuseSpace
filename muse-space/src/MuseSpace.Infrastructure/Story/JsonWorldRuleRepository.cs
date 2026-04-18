using Microsoft.Extensions.Options;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Story;

public sealed class JsonWorldRuleRepository : JsonRepositoryBase, IWorldRuleRepository
{
    private readonly string _basePath;

    public JsonWorldRuleRepository(IOptions<DataOptions> options)
        => _basePath = options.Value.BasePath;

    private string FilePath(Guid projectId) => GetProjectFilePath(_basePath, projectId, "world-rules.json");

    public Task<List<WorldRule>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => ReadFileAsync<WorldRule>(FilePath(projectId), cancellationToken);

    public async Task<WorldRule?> GetByIdAsync(Guid projectId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        var all = await GetByProjectAsync(projectId, cancellationToken);
        return all.FirstOrDefault(r => r.Id == ruleId);
    }

    public async Task SaveAsync(Guid projectId, WorldRule rule, CancellationToken cancellationToken = default)
    {
        var all = await GetByProjectAsync(projectId, cancellationToken);
        var index = all.FindIndex(r => r.Id == rule.Id);
        if (index >= 0) all[index] = rule;
        else all.Add(rule);
        await WriteFileAsync(FilePath(projectId), all, cancellationToken);
    }

    public async Task DeleteAsync(Guid projectId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        var all = await GetByProjectAsync(projectId, cancellationToken);
        all.RemoveAll(r => r.Id == ruleId);
        await WriteFileAsync(FilePath(projectId), all, cancellationToken);
    }
}
