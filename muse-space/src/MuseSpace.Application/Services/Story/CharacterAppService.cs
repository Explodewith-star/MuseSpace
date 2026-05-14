using Mapster;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Contracts.Characters;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Services.Story;

public sealed class CharacterAppService
{
    private readonly ICharacterRepository _repository;

    public CharacterAppService(ICharacterRepository repository)
        => _repository = repository;

    public async Task<CharacterResponse> CreateAsync(Guid projectId, CreateCharacterRequest request, CancellationToken cancellationToken = default)
    {
        var character = request.Adapt<Character>();
        character.Id = Guid.NewGuid();
        character.StoryProjectId = projectId;
        await _repository.SaveAsync(projectId, character, cancellationToken);
        return character.Adapt<CharacterResponse>();
    }

    public async Task<List<CharacterResponse>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var characters = await _repository.GetByProjectAsync(projectId, cancellationToken);
        return characters.Adapt<List<CharacterResponse>>();
    }

    public async Task<CharacterResponse?> GetByIdAsync(Guid projectId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var character = await _repository.GetByIdAsync(projectId, characterId, cancellationToken);
        return character?.Adapt<CharacterResponse>();
    }

    public async Task<bool> DeleteAsync(Guid projectId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(projectId, characterId, cancellationToken);
        if (existing is null) return false;
        await _repository.DeleteAsync(projectId, characterId, cancellationToken);
        return true;
    }

    public async Task<CharacterResponse?> UpdateAsync(Guid projectId, Guid characterId, UpdateCharacterRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(projectId, characterId, cancellationToken);
        if (existing is null) return null;

        if (request.Name is not null) existing.Name = request.Name;
        if (request.Age.HasValue) existing.Age = request.Age;
        if (request.Role is not null) existing.Role = request.Role;
        if (request.PersonalitySummary is not null) existing.PersonalitySummary = request.PersonalitySummary;
        if (request.Motivation is not null) existing.Motivation = request.Motivation;
        if (request.SpeakingStyle is not null) existing.SpeakingStyle = request.SpeakingStyle;
        if (request.ForbiddenBehaviors is not null) existing.ForbiddenBehaviors = request.ForbiddenBehaviors;
        if (request.PublicSecrets is not null) existing.PublicSecrets = request.PublicSecrets;
        if (request.PrivateSecrets is not null) existing.PrivateSecrets = request.PrivateSecrets;
        if (request.CurrentState is not null) existing.CurrentState = request.CurrentState;
        if (request.Tags is not null) existing.Tags = request.Tags;
        if (request.Category is not null) existing.Category = request.Category;

        await _repository.SaveAsync(projectId, existing, cancellationToken);
        return existing.Adapt<CharacterResponse>();
    }
}
