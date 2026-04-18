using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IWorldRuleRepository
{
    Task<List<WorldRule>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<WorldRule?> GetByIdAsync(Guid projectId, Guid ruleId, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid projectId, WorldRule rule, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid projectId, Guid ruleId, CancellationToken cancellationToken = default);
}
