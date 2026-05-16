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

    // ── 大纲角色 CRUD ──────────────────────────────────────────────────────

    public async Task<CharacterResponse> CreateAsync(Guid projectId, Guid outlineId, CreateCharacterRequest request, CancellationToken cancellationToken = default)
    {
        var character = request.Adapt<Character>();
        character.Id = Guid.NewGuid();
        character.StoryProjectId = projectId;
        character.StoryOutlineId = outlineId;
        await _repository.SaveAsync(projectId, character, cancellationToken);
        return character.Adapt<CharacterResponse>();
    }

    public async Task<List<CharacterResponse>> GetByOutlineAsync(Guid outlineId, CancellationToken cancellationToken = default)
    {
        var characters = await _repository.GetByOutlineAsync(outlineId, cancellationToken);
        return characters.Adapt<List<CharacterResponse>>();
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

        await _repository.SaveAsync(projectId, existing, cancellationToken);
        return existing.Adapt<CharacterResponse>();
    }

    // ── 原著角色池 ────────────────────────────────────────────────────────

    /// <summary>获取项目的原著角色池（StoryOutlineId IS NULL）。</summary>
    public async Task<List<CharacterResponse>> GetPoolAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var characters = await _repository.GetPoolByProjectAsync(projectId, cancellationToken);
        return characters.Adapt<List<CharacterResponse>>();
    }

    /// <summary>
    /// 将原著角色池中的角色引入到指定大纲（隔离复制，各自独立演化）。
    /// 不修改原著池中的原始记录。
    /// </summary>
    public async Task<List<CharacterResponse>> ImportFromPoolAsync(
        Guid projectId, Guid outlineId, List<Guid> characterIds, CancellationToken cancellationToken = default)
    {
        var copies = new List<Character>();
        foreach (var charId in characterIds)
        {
            var source = await _repository.GetByIdAsync(projectId, charId, cancellationToken);
            // 只允许从原著池（null）引入
            if (source is null || source.StoryOutlineId is not null) continue;

            var copy = source.Adapt<Character>();
            copy.Id = Guid.NewGuid();
            copy.StoryOutlineId = outlineId;
            copies.Add(copy);
        }

        if (copies.Count > 0)
            await _repository.SaveManyAsync(projectId, copies, cancellationToken);

        return copies.Adapt<List<CharacterResponse>>();
    }

    /// <summary>将角色从一个大纲复制到另一个大纲（横向共亭）。</summary>
    public async Task<List<CharacterResponse>> CopyToOutlineAsync(Guid projectId, CopyCharactersRequest request, CancellationToken cancellationToken = default)
    {
        var copies = new List<Character>();
        foreach (var charId in request.CharacterIds)
        {
            var source = await _repository.GetByIdAsync(projectId, charId, cancellationToken);
            if (source is null) continue;

            var copy = source.Adapt<Character>();
            copy.Id = Guid.NewGuid();
            copy.StoryOutlineId = request.TargetOutlineId;
            copies.Add(copy);
        }

        if (copies.Count > 0)
            await _repository.SaveManyAsync(projectId, copies, cancellationToken);

        return copies.Adapt<List<CharacterResponse>>();
    }
}
