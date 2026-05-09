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
    private readonly IStoryOutlineRepository _outlineRepository;

    public OutlineSuggestionApplier(
        IChapterRepository chapterRepository,
        IStoryOutlineRepository outlineRepository)
    {
        _chapterRepository = chapterRepository;
        _outlineRepository = outlineRepository;
    }

    public string Category => SuggestionCategories.Outline;

    public async Task<Guid> ApplyAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // 优先解析为分卷结构 { volumes: [{ chapters: [...] }] }
        var chapters = new List<OutlineChapterPayload>();
        try
        {
            var payload = JsonSerializer.Deserialize<OutlinePayload>(suggestion.ContentJson, opts);
            if (payload?.Volumes is { Count: > 0 } volumes)
            {
                foreach (var vol in volumes)
                    chapters.AddRange(vol.Chapters ?? []);
            }
        }
        catch
        {
            // 忽略，回退到平铺数组
        }

        // 兼容老格式（平铺章节数组）
        if (chapters.Count == 0)
        {
            try
            {
                chapters = JsonSerializer.Deserialize<List<OutlineChapterPayload>>(suggestion.ContentJson, opts) ?? [];
            }
            catch
            {
                throw new InvalidOperationException("大纲内容 JSON 解析失败");
            }
        }

        if (chapters.Count == 0)
            throw new InvalidOperationException("大纲为空，无可导入章节");

        var outline = suggestion.TargetEntityId.HasValue
            ? await _outlineRepository.GetByIdAsync(
                suggestion.StoryProjectId, suggestion.TargetEntityId.Value, cancellationToken)
            : null;
        outline ??= await _outlineRepository.GetOrCreateDefaultAsync(
            suggestion.StoryProjectId, cancellationToken);

        foreach (var item in chapters)
        {
            var chapter = new Chapter
            {
                Id = Guid.NewGuid(),
                StoryProjectId = suggestion.StoryProjectId,
                StoryOutlineId = outline.Id,
                Number = item.Number,
                Title = item.Title,
                Goal = item.Goal,
                Summary = item.Summary,
                KeyCharacterIds = new List<Guid>(),
                MustIncludePoints = new List<string>(),
                SourceSuggestionId = suggestion.Id,
            };

            await _chapterRepository.SaveAsync(suggestion.StoryProjectId, chapter, cancellationToken);
        }

        return outline.Id;
    }

    /// <summary>
    /// 删除由此大纲建议导入的所有章节（包含其中已有草稿的章节）。
    /// </summary>
    public async Task RetractAsync(AgentSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        if (suggestion.TargetEntityId.HasValue)
        {
            await _chapterRepository.DeleteBySourceSuggestionIdAsync(
                suggestion.Id, suggestion.TargetEntityId.Value, cancellationToken);
            return;
        }

        await _chapterRepository.DeleteBySourceSuggestionIdAsync(
            suggestion.Id, cancellationToken);
    }

    private sealed class OutlinePayload
    {
        public List<OutlineVolumePayload> Volumes { get; set; } = [];
    }

    private sealed class OutlineVolumePayload
    {
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public List<OutlineChapterPayload> Chapters { get; set; } = [];
    }

    private sealed class OutlineChapterPayload
    {
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }
}
