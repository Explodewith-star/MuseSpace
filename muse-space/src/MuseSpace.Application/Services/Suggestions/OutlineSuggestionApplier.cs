using System.Text.Json;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Abstractions.Suggestions;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Services.Suggestions;

/// <summary>
/// 大纲建议的 Apply 逻辑。
/// 解析 ContentJson 中的章节数组，批量创建 Chapter 实体。
/// </summary>
public sealed class OutlineSuggestionApplier : ISuggestionApplier
{
    private readonly IChapterRepository _chapterRepository;

    public OutlineSuggestionApplier(IChapterRepository chapterRepository)
        => _chapterRepository = chapterRepository;

    public string Category => SuggestionCategories.Outline;

    public async Task<Guid> ApplyAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var items = JsonSerializer.Deserialize<List<OutlineChapterPayload>>(suggestion.ContentJson, opts)
            ?? throw new InvalidOperationException("大纲内容 JSON 解析失败");

        foreach (var item in items)
        {
            var chapter = new Chapter
            {
                Id = Guid.NewGuid(),
                StoryProjectId = suggestion.StoryProjectId,
                Number = item.Number,
                Title = item.Title,
                Goal = item.Goal,
                Summary = item.Summary,
            };

            await _chapterRepository.SaveAsync(suggestion.StoryProjectId, chapter, cancellationToken);
        }

        return suggestion.Id;
    }

    private sealed class OutlineChapterPayload
    {
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }
}
