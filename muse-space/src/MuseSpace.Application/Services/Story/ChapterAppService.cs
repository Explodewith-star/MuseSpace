using Mapster;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Contracts.Chapters;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Services.Story;

public sealed class ChapterAppService
{
    private readonly IChapterRepository _repository;

    public ChapterAppService(IChapterRepository repository)
        => _repository = repository;

    public async Task<ChapterResponse> CreateAsync(Guid projectId, CreateChapterRequest request, CancellationToken cancellationToken = default)
    {
        var chapter = request.Adapt<Chapter>();
        chapter.Id = Guid.NewGuid();
        chapter.StoryProjectId = projectId;
        await _repository.SaveAsync(projectId, chapter, cancellationToken);
        return chapter.Adapt<ChapterResponse>();
    }

    public async Task<List<ChapterResponse>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var chapters = await _repository.GetByProjectAsync(projectId, cancellationToken);
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
}
