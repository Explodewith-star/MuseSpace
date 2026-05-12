using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Contracts.Outlines;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

namespace MuseSpace.Application.Services.Story;

public sealed class StoryOutlineAppService
{
    private readonly IStoryOutlineRepository _outlineRepository;
    private readonly IChapterRepository _chapterRepository;

    public StoryOutlineAppService(
        IStoryOutlineRepository outlineRepository,
        IChapterRepository chapterRepository)
    {
        _outlineRepository = outlineRepository;
        _chapterRepository = chapterRepository;
    }

    public async Task<List<StoryOutlineResponse>> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var outlines = await _outlineRepository.GetByProjectAsync(projectId, cancellationToken);
        if (outlines.Count == 0)
            outlines = [await _outlineRepository.GetOrCreateDefaultAsync(projectId, cancellationToken)];

        var chapters = await _chapterRepository.GetByProjectAsync(projectId, cancellationToken);
        var chapterCounts = chapters
            .GroupBy(c => c.StoryOutlineId)
            .ToDictionary(g => g.Key, g => g.Count());

        return outlines.Select(o => ToResponse(o, chapterCounts.GetValueOrDefault(o.Id))).ToList();
    }

    public async Task<StoryOutlineResponse?> GetByIdAsync(
        Guid projectId,
        Guid outlineId,
        CancellationToken cancellationToken = default)
    {
        var outline = await _outlineRepository.GetByIdAsync(projectId, outlineId, cancellationToken);
        if (outline is null) return null;

        var chapters = await _chapterRepository.GetByProjectAsync(projectId, cancellationToken);
        var count = chapters.Count(c => c.StoryOutlineId == outline.Id);
        return ToResponse(outline, count);
    }

    public async Task<StoryOutlineResponse> CreateAsync(
        Guid projectId,
        CreateStoryOutlineRequest request,
        CancellationToken cancellationToken = default)
    {
        // 如果指定了 ChainId，自动计算 ChainIndex
        int chainIndex = 0;
        if (request.ChainId.HasValue)
        {
            var allOutlines = await _outlineRepository.GetByProjectAsync(projectId, cancellationToken);
            chainIndex = allOutlines.Count(o => o.ChainId == request.ChainId.Value) + 1;
        }

        var outline = new StoryOutline
        {
            Id = Guid.NewGuid(),
            StoryProjectId = projectId,
            Name = string.IsNullOrWhiteSpace(request.Name)
                ? BuildDefaultName(request.Mode)
                : request.Name.Trim(),
            Mode = ParseEnum(request.Mode, GenerationMode.Original),
            ChainId = request.ChainId,
            ChainIndex = chainIndex,
            PreviousOutlineId = request.PreviousOutlineId,
            SourceNovelId = request.SourceNovelId,
            SourceRangeStart = request.SourceRangeStart,
            SourceRangeEnd = request.SourceRangeEnd,
            BranchTopic = request.BranchTopic?.Trim(),
            ContinuationAnchor = request.ContinuationAnchor?.Trim(),
            DivergencePolicy = ParseEnum(request.DivergencePolicy, DivergencePolicy.SoftCanon),
            TargetChapterCount = request.TargetChapterCount,
            OutlineSummary = request.OutlineSummary?.Trim(),
            IsDefault = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _outlineRepository.SaveAsync(projectId, outline, cancellationToken);
        return ToResponse(outline, 0);
    }

    public async Task<StoryOutlineResponse?> UpdateAsync(
        Guid projectId,
        Guid outlineId,
        UpdateStoryOutlineRequest request,
        CancellationToken cancellationToken = default)
    {
        var outline = await _outlineRepository.GetByIdAsync(projectId, outlineId, cancellationToken);
        if (outline is null) return null;

        if (!string.IsNullOrWhiteSpace(request.Name)) outline.Name = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.Mode))
            outline.Mode = ParseEnum(request.Mode, outline.Mode);
        if (request.SourceNovelId.HasValue) outline.SourceNovelId = request.SourceNovelId;
        if (request.SourceRangeStart.HasValue) outline.SourceRangeStart = request.SourceRangeStart;
        if (request.SourceRangeEnd.HasValue) outline.SourceRangeEnd = request.SourceRangeEnd;
        if (request.BranchTopic is not null) outline.BranchTopic = request.BranchTopic.Trim();
        if (request.ContinuationAnchor is not null) outline.ContinuationAnchor = request.ContinuationAnchor.Trim();
        if (!string.IsNullOrWhiteSpace(request.DivergencePolicy))
            outline.DivergencePolicy = ParseEnum(request.DivergencePolicy, outline.DivergencePolicy);
        if (request.TargetChapterCount.HasValue) outline.TargetChapterCount = request.TargetChapterCount;
        if (request.OutlineSummary is not null) outline.OutlineSummary = request.OutlineSummary.Trim();

        await _outlineRepository.SaveAsync(projectId, outline, cancellationToken);

        var chapters = await _chapterRepository.GetByProjectAsync(projectId, cancellationToken);
        return ToResponse(outline, chapters.Count(c => c.StoryOutlineId == outline.Id));
    }

    public async Task<bool> DeleteAsync(
        Guid projectId,
        Guid outlineId,
        CancellationToken cancellationToken = default)
    {
        var outline = await _outlineRepository.GetByIdAsync(projectId, outlineId, cancellationToken);
        if (outline is null || outline.IsDefault) return false;

        await _outlineRepository.DeleteAsync(projectId, outlineId, cancellationToken);
        return true;
    }

    private static StoryOutlineResponse ToResponse(StoryOutline outline, int chapterCount)
        => new()
        {
            Id = outline.Id,
            StoryProjectId = outline.StoryProjectId,
            Name = outline.Name,
            Mode = outline.Mode.ToString(),
            ChainId = outline.ChainId,
            ChainIndex = outline.ChainIndex,
            PreviousOutlineId = outline.PreviousOutlineId,
            SourceNovelId = outline.SourceNovelId,
            SourceRangeStart = outline.SourceRangeStart,
            SourceRangeEnd = outline.SourceRangeEnd,
            BranchTopic = outline.BranchTopic,
            ContinuationAnchor = outline.ContinuationAnchor,
            DivergencePolicy = outline.DivergencePolicy.ToString(),
            TargetChapterCount = outline.TargetChapterCount,
            OutlineSummary = outline.OutlineSummary,
            IsDefault = outline.IsDefault,
            CreatedAt = outline.CreatedAt,
            UpdatedAt = outline.UpdatedAt,
            ChapterCount = chapterCount,
        };

    private static TEnum ParseEnum<TEnum>(string? raw, TEnum fallback)
        where TEnum : struct
        => Enum.TryParse<TEnum>(raw, ignoreCase: true, out var value) ? value : fallback;

    private static string BuildDefaultName(string? mode)
        => ParseEnum(mode, GenerationMode.Original) switch
        {
            GenerationMode.ContinueFromOriginal => "原著续写",
            GenerationMode.SideStoryFromOriginal => "支线番外",
            GenerationMode.ExpandOrRewrite => "扩写改写",
            _ => "原创主线",
        };
}
