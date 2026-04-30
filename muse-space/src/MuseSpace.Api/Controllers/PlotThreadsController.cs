using Microsoft.AspNetCore.Mvc;
using MuseSpace.Api.Authorization;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.PlotThreads;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

namespace MuseSpace.Api.Controllers;

/// <summary>
/// D4-C 伏笔追踪：PlotThread CRUD。
/// </summary>
[ApiController]
[ProjectAccess]
[Route("api/projects/{projectId:guid}/plot-threads")]
public class PlotThreadsController : ControllerBase
{
    private readonly IPlotThreadRepository _repo;
    public PlotThreadsController(IPlotThreadRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<PlotThreadResponse>>>> GetAll(
        Guid projectId, CancellationToken ct)
    {
        var list = await _repo.GetByProjectAsync(projectId, ct);
        return Ok(ApiResponse<List<PlotThreadResponse>>.Ok(list.Select(ToResp).ToList()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PlotThreadResponse>>> GetOne(
        Guid projectId, Guid id, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(projectId, id, ct);
        return item is null
            ? NotFound(ApiResponse<PlotThreadResponse>.Fail("线索不存在"))
            : Ok(ApiResponse<PlotThreadResponse>.Ok(ToResp(item)));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PlotThreadResponse>>> Create(
        Guid projectId, [FromBody] UpsertPlotThreadRequest req, CancellationToken ct)
    {
        var thread = new PlotThread
        {
            StoryProjectId = projectId,
            Title = req.Title,
            Description = req.Description,
            Importance = req.Importance,
            Status = ParseStatus(req.Status, ForeshadowingStatus.Introduced),
            PlantedInChapterId = req.PlantedInChapterId,
            ResolvedInChapterId = req.ResolvedInChapterId,
            RelatedCharacterIds = req.RelatedCharacterIds,
            ExpectedResolveByChapterNumber = req.ExpectedResolveByChapterNumber,
            Tags = req.Tags,
        };
        var saved = await _repo.AddAsync(thread, ct);
        return Ok(ApiResponse<PlotThreadResponse>.Ok(ToResp(saved)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PlotThreadResponse>>> Update(
        Guid projectId, Guid id, [FromBody] UpsertPlotThreadRequest req, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(projectId, id, ct);
        if (item is null) return NotFound(ApiResponse<PlotThreadResponse>.Fail("线索不存在"));

        item.Title = req.Title;
        item.Description = req.Description;
        item.Importance = req.Importance;
        item.Status = ParseStatus(req.Status, item.Status);
        item.PlantedInChapterId = req.PlantedInChapterId;
        item.ResolvedInChapterId = req.ResolvedInChapterId;
        item.RelatedCharacterIds = req.RelatedCharacterIds;
        item.ExpectedResolveByChapterNumber = req.ExpectedResolveByChapterNumber;
        item.Tags = req.Tags;

        await _repo.UpdateAsync(item, ct);
        return Ok(ApiResponse<PlotThreadResponse>.Ok(ToResp(item)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        Guid projectId, Guid id, CancellationToken ct)
    {
        await _repo.DeleteAsync(projectId, id, ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    private static ForeshadowingStatus ParseStatus(string? raw, ForeshadowingStatus fallback)
        => Enum.TryParse<ForeshadowingStatus>(raw, true, out var s) ? s : fallback;

    private static PlotThreadResponse ToResp(PlotThread t) => new()
    {
        Id = t.Id,
        StoryProjectId = t.StoryProjectId,
        Title = t.Title,
        Description = t.Description,
        Importance = t.Importance,
        Status = t.Status.ToString(),
        PlantedInChapterId = t.PlantedInChapterId,
        ResolvedInChapterId = t.ResolvedInChapterId,
        RelatedCharacterIds = t.RelatedCharacterIds,
        ExpectedResolveByChapterNumber = t.ExpectedResolveByChapterNumber,
        Tags = t.Tags,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
    };
}
