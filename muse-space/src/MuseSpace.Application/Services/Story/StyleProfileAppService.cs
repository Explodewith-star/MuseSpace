using Mapster;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Contracts.StyleProfiles;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Services.Story;

public sealed class StyleProfileAppService
{
    private readonly IStyleProfileRepository _repository;

    public StyleProfileAppService(IStyleProfileRepository repository)
        => _repository = repository;

    public async Task<StyleProfileResponse> UpsertAsync(Guid projectId, UpsertStyleProfileRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByProjectAsync(projectId, cancellationToken);
        var profile = request.Adapt<StyleProfile>();
        profile.Id = existing?.Id ?? Guid.NewGuid();
        profile.StoryProjectId = projectId;
        await _repository.SaveAsync(projectId, profile, cancellationToken);
        return profile.Adapt<StyleProfileResponse>();
    }

    public async Task<StyleProfileResponse?> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetByProjectAsync(projectId, cancellationToken);
        return profile?.Adapt<StyleProfileResponse>();
    }
}
