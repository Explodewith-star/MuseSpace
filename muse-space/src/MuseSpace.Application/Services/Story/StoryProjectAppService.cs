using Mapster;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Contracts.Story;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Services.Story;

public sealed class StoryProjectAppService
{
    private readonly IStoryProjectRepository _repository;

    public StoryProjectAppService(IStoryProjectRepository repository)
        => _repository = repository;

    public async Task<StoryProjectResponse> CreateAsync(CreateStoryProjectRequest request, Guid? userId, CancellationToken cancellationToken = default)
    {
        var project = request.Adapt<StoryProject>();
        project.Id = Guid.NewGuid();
        project.UserId = userId;
        project.CreatedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveAsync(project, cancellationToken);
        return project.Adapt<StoryProjectResponse>();
    }

    public async Task<StoryProjectResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetByIdAsync(id, cancellationToken);
        return project?.Adapt<StoryProjectResponse>();
    }

    public async Task<List<StoryProjectResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _repository.GetAllAsync(cancellationToken);
        return projects.Adapt<List<StoryProjectResponse>>();
    }

    public async Task<List<StoryProjectResponse>> GetByUserIdAsync(Guid? userId, CancellationToken cancellationToken = default)
    {
        var projects = await _repository.GetByUserIdAsync(userId, cancellationToken);
        return projects.Adapt<List<StoryProjectResponse>>();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return false;
        await _repository.DeleteAsync(id, cancellationToken);
        return true;
    }
}
