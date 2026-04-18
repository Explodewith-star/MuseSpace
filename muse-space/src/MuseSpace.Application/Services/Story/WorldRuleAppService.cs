using Mapster;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Contracts.WorldRules;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Services.Story;

public sealed class WorldRuleAppService
{
    private readonly IWorldRuleRepository _repository;

    public WorldRuleAppService(IWorldRuleRepository repository)
        => _repository = repository;

    public async Task<WorldRuleResponse> CreateAsync(Guid projectId, CreateWorldRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = request.Adapt<WorldRule>();
        rule.Id = Guid.NewGuid();
        rule.StoryProjectId = projectId;
        await _repository.SaveAsync(projectId, rule, cancellationToken);
        return rule.Adapt<WorldRuleResponse>();
    }

    public async Task<List<WorldRuleResponse>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var rules = await _repository.GetByProjectAsync(projectId, cancellationToken);
        return rules.Adapt<List<WorldRuleResponse>>();
    }

    public async Task<WorldRuleResponse?> GetByIdAsync(Guid projectId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        var rule = await _repository.GetByIdAsync(projectId, ruleId, cancellationToken);
        return rule?.Adapt<WorldRuleResponse>();
    }

    public async Task<bool> DeleteAsync(Guid projectId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(projectId, ruleId, cancellationToken);
        if (existing is null) return false;
        await _repository.DeleteAsync(projectId, ruleId, cancellationToken);
        return true;
    }

    public async Task<WorldRuleResponse?> UpdateAsync(Guid projectId, Guid ruleId, UpdateWorldRuleRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(projectId, ruleId, cancellationToken);
        if (existing is null) return null;

        if (request.Title is not null) existing.Title = request.Title;
        if (request.Description is not null) existing.Description = request.Description;
        if (request.Category is not null) existing.Category = request.Category;
        if (request.Priority.HasValue) existing.Priority = request.Priority.Value;
        if (request.IsHardConstraint.HasValue) existing.IsHardConstraint = request.IsHardConstraint.Value;

        await _repository.SaveAsync(projectId, existing, cancellationToken);
        return existing.Adapt<WorldRuleResponse>();
    }
}
