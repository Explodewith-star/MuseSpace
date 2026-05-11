using System.Text.Json;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Abstractions.Suggestions;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Services.Suggestions;

/// <summary>
/// 把 Category=Character 的建议写入 characters 表。
/// ContentJson 格式与 Character 实体字段对齐。
/// </summary>
public sealed class CharacterSuggestionApplier : ISuggestionApplier
{
    private readonly ICharacterRepository _characterRepository;

    public CharacterSuggestionApplier(ICharacterRepository characterRepository)
        => _characterRepository = characterRepository;

    public string Category => SuggestionCategories.Character;

    public async Task<Guid> ApplyAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<CharacterPayload>(suggestion.ContentJson, opts)
            ?? throw new InvalidOperationException("建议内容 JSON 解析失败");

        var character = new Character
        {
            Id = suggestion.TargetEntityId ?? Guid.NewGuid(),
            StoryProjectId = suggestion.StoryProjectId,
            SourceNovelId = suggestion.SourceNovelId,
            Name = data.Name ?? "未命名角色",
            Age = data.Age,
            Role = data.Role,
            PersonalitySummary = data.PersonalitySummary,
            Motivation = data.Motivation,
            SpeakingStyle = data.SpeakingStyle,
            ForbiddenBehaviors = data.ForbiddenBehaviors,
            CurrentState = data.CurrentState,
            Tags = data.Tags,
        };

        await _characterRepository.SaveAsync(suggestion.StoryProjectId, character, cancellationToken);
        return character.Id;
    }

    public async Task RetractAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        if (suggestion.TargetEntityId.HasValue)
            await _characterRepository.DeleteAsync(suggestion.StoryProjectId, suggestion.TargetEntityId.Value, cancellationToken);
    }

    /// <summary>ContentJson 的内部解析模型。</summary>
    private sealed class CharacterPayload
    {
        public string? Name { get; set; }
        public int? Age { get; set; }
        public string? Role { get; set; }
        public string? PersonalitySummary { get; set; }
        public string? Motivation { get; set; }
        public string? SpeakingStyle { get; set; }
        public string? ForbiddenBehaviors { get; set; }
        public string? CurrentState { get; set; }
        public string? Tags { get; set; }
    }
}
