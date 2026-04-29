using System.Text.Json;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Abstractions.Suggestions;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Services.Suggestions;

/// <summary>
/// 把 Category=StyleProfile 的建议写入 style_profiles 表。
/// </summary>
public sealed class StyleProfileSuggestionApplier : ISuggestionApplier
{
    private readonly IStyleProfileRepository _styleProfileRepository;

    public StyleProfileSuggestionApplier(IStyleProfileRepository styleProfileRepository)
        => _styleProfileRepository = styleProfileRepository;

    public string Category => SuggestionCategories.StyleProfile;

    public async Task<Guid> ApplyAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<StyleProfilePayload>(suggestion.ContentJson, opts)
            ?? throw new InvalidOperationException("建议内容 JSON 解析失败");

        var profile = new StyleProfile
        {
            Id = suggestion.TargetEntityId ?? Guid.NewGuid(),
            StoryProjectId = suggestion.StoryProjectId,
            Name = data.Name ?? "提取文风",
            Tone = data.Tone,
            SentenceLengthPreference = data.SentenceLengthPreference,
            DialogueRatio = data.DialogueRatio,
            DescriptionDensity = data.DescriptionDensity,
            ForbiddenExpressions = data.ForbiddenExpressions,
            SampleReferenceText = data.SampleReferenceText,
        };

        await _styleProfileRepository.SaveAsync(suggestion.StoryProjectId, profile, cancellationToken);
        return profile.Id;
    }

    public async Task RetractAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        if (suggestion.TargetEntityId.HasValue)
            await _styleProfileRepository.DeleteAsync(suggestion.StoryProjectId, suggestion.TargetEntityId.Value, cancellationToken);
    }

    private sealed class StyleProfilePayload
    {
        public string? Name { get; set; }
        public string? Tone { get; set; }
        public string? SentenceLengthPreference { get; set; }
        public string? DialogueRatio { get; set; }
        public string? DescriptionDensity { get; set; }
        public string? ForbiddenExpressions { get; set; }
        public string? SampleReferenceText { get; set; }
    }
}
