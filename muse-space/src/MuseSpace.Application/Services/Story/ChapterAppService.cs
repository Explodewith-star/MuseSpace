using Mapster;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Contracts.Chapters;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;
namespace MuseSpace.Application.Services.Story;

public sealed class ChapterAppService
{
    private readonly IChapterRepository _repository;
    private readonly IStoryOutlineRepository _outlineRepository;

    public ChapterAppService(IChapterRepository repository, IStoryOutlineRepository outlineRepository)
    {
        _repository = repository;
        _outlineRepository = outlineRepository;
    }

    public async Task<ChapterResponse> CreateAsync(Guid projectId, CreateChapterRequest request, CancellationToken cancellationToken = default)
    {
        var chapter = request.Adapt<Chapter>();
        chapter.Id = Guid.NewGuid();
        chapter.StoryProjectId = projectId;
        var outline = request.StoryOutlineId.HasValue
            ? await _outlineRepository.GetByIdAsync(projectId, request.StoryOutlineId.Value, cancellationToken)
            : await _outlineRepository.GetOrCreateDefaultAsync(projectId, cancellationToken);
        chapter.StoryOutlineId = outline?.Id
            ?? throw new InvalidOperationException("故事大纲不存在");
        if (request.AllowedRevealLevel.HasValue)
            chapter.AllowedRevealLevel = (ChapterRevealLevel)request.AllowedRevealLevel.Value;
        // Mapster 可能把可空集合映射为 null，需确保非空
        chapter.KeyCharacterIds ??= new List<Guid>();
        chapter.MustIncludePoints ??= new List<string>();
        await _repository.SaveAsync(projectId, chapter, cancellationToken);
        return chapter.Adapt<ChapterResponse>();
    }

    public async Task<List<ChapterResponse>> GetByProjectAsync(
        Guid projectId,
        Guid? storyOutlineId = null,
        CancellationToken cancellationToken = default)
    {
        var chapters = storyOutlineId.HasValue
            ? await _repository.GetByOutlineAsync(projectId, storyOutlineId.Value, cancellationToken)
            : await _repository.GetByProjectAsync(projectId, cancellationToken);
        return chapters.OrderBy(c => c.Number).Adapt<List<ChapterResponse>>();
    }

    public async Task<ChapterResponse?> GetByIdAsync(Guid projectId, Guid chapterId, CancellationToken cancellationToken = default)
    {
        var chapter = await _repository.GetByIdAsync(projectId, chapterId, cancellationToken);
        return chapter?.Adapt<ChapterResponse>();
    }

    public async Task<bool> DeleteAsync(Guid projectId, Guid chapterId, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(projectId, chapterId, cancellationToken);
        if (existing is null) return false;
        await _repository.DeleteAsync(projectId, chapterId, cancellationToken);
        return true;
    }

    /// <summary>批量删除章节（含关联 Scene / 草稿 / 定稿）。返回实际删除数量。</summary>
    public async Task<int> BatchDeleteAsync(Guid projectId, IEnumerable<Guid> chapterIds, CancellationToken cancellationToken = default)
        => await _repository.BatchDeleteAsync(projectId, chapterIds, cancellationToken);

    /// <summary>
    /// 批量重排章节 Number。<paramref name="orderedChapterIds"/> 顺序决定目标编号（首项 → <paramref name="startNumber"/>）。
    /// 用于消除"删除章节后编号空洞"。返回实际更新数量。
    /// </summary>
    public async Task<int> BatchReorderAsync(
        Guid projectId,
        IReadOnlyList<Guid> orderedChapterIds,
        int startNumber = 1,
        CancellationToken cancellationToken = default)
        => await _repository.BatchReorderAsync(projectId, orderedChapterIds, startNumber, cancellationToken);

    public async Task<int> BatchReorderAsync(
        Guid projectId,
        Guid storyOutlineId,
        IReadOnlyList<Guid> orderedChapterIds,
        int startNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var outline = await _outlineRepository.GetByIdAsync(projectId, storyOutlineId, cancellationToken);
        if (outline is null) throw new InvalidOperationException("故事大纲不存在");
        return await _repository.BatchReorderAsync(
            projectId, storyOutlineId, orderedChapterIds, startNumber, cancellationToken);
    }

    public async Task<ChapterResponse?> UpdateAsync(Guid projectId, Guid chapterId, UpdateChapterRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(projectId, chapterId, cancellationToken);
        if (existing is null) return null;

        if (request.Title is not null) existing.Title = request.Title;
        if (request.Summary is not null) existing.Summary = request.Summary;
        if (request.Goal is not null) existing.Goal = request.Goal;
        if (request.DraftText is not null) existing.DraftText = request.DraftText;
        if (request.FinalText is not null) existing.FinalText = request.FinalText;
        if (request.Status.HasValue) existing.Status = (MuseSpace.Domain.Enums.ChapterStatus)request.Status.Value;
        if (request.AllowedRevealLevel.HasValue)
            existing.AllowedRevealLevel = (ChapterRevealLevel)request.AllowedRevealLevel.Value;
        if (request.Conflict is not null) existing.Conflict = request.Conflict;
        if (request.EmotionCurve is not null) existing.EmotionCurve = request.EmotionCurve;
        if (request.KeyCharacterIds is not null) existing.KeyCharacterIds = request.KeyCharacterIds;
        if (request.MustIncludePoints is not null) existing.MustIncludePoints = request.MustIncludePoints;

        await _repository.SaveAsync(projectId, existing, cancellationToken);
        return existing.Adapt<ChapterResponse>();
    }

    /// <summary>
    /// 一键将 DraftText 采用为 FinalText：
    /// - 草稿为空 → 返回 (null, DraftEmpty)。
    /// - 定稿已有内容且未指定覆盖 → 返回 (null, ExistingFinalConflict)，前端弹二次确认。
    /// - 否则写入 FinalText，状态升至 Finalized（若未到此级），保留 DraftText 用于对照。
    /// </summary>
    public async Task<(AdoptDraftResponse? Response, string? FailureReason)> AdoptDraftAsync(
        Guid projectId,
        Guid chapterId,
        bool overrideExisting,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(projectId, chapterId, cancellationToken);
        if (existing is null) return (null, null);

        var draftLength = existing.DraftText?.Length ?? 0;
        var previousFinalLength = existing.FinalText?.Length ?? 0;

        if (string.IsNullOrWhiteSpace(existing.DraftText))
        {
            return (null, AdoptDraftFailureReasons.DraftEmpty);
        }

        if (previousFinalLength > 0 && !overrideExisting)
        {
            return (
                new AdoptDraftResponse
                {
                    Adopted = false,
                    DraftLength = draftLength,
                    PreviousFinalLength = previousFinalLength,
                    FinalLength = previousFinalLength,
                },
                AdoptDraftFailureReasons.ExistingFinalConflict);
        }

        existing.FinalText = existing.DraftText;
        if (existing.Status < ChapterStatus.Finalized)
        {
            existing.Status = ChapterStatus.Finalized;
        }

        await _repository.SaveAsync(projectId, existing, cancellationToken);

        return (
            new AdoptDraftResponse
            {
                Adopted = true,
                DraftLength = draftLength,
                PreviousFinalLength = previousFinalLength,
                FinalLength = existing.FinalText?.Length ?? 0,
            },
            null);
    }
}
