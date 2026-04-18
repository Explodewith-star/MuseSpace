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
}
