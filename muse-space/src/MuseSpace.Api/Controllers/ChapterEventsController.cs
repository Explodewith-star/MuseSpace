using Microsoft.AspNetCore.Mvc;
using MuseSpace.Api.Authorization;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Contracts.CanonFacts;
using MuseSpace.Contracts.Common;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Api.Controllers;

/// <summary>
/// Module D 正典事实层：章节事件（时间线）CRUD。
/// </summary>
[ApiController]
[ProjectAccess]
[Route("api/projects/{projectId:guid}/chapters/{chapterId:guid}/events")]
public class ChapterEventsController : ControllerBase
{
    private readonly IChapterEventRepository _repo;
    public ChapterEventsController(IChapterEventRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ChapterEventResponse>>>> GetByChapter(
        Guid projectId, Guid chapterId, CancellationToken ct)
    {
        var list = await _repo.GetByChapterAsync(projectId, chapterId, ct);
        return Ok(ApiResponse<List<ChapterEventResponse>>.Ok(list.Select(ToResp).ToList()));
    }

    /// <summary>批量替换本章事件（PUT 语义）。</summary>
    [HttpPut]
    public async Task<ActionResult<ApiResponse<List<ChapterEventResponse>>>> Replace(
        Guid projectId, Guid chapterId, [FromBody] ReplaceChapterEventsRequest req, CancellationToken ct)
    {
        var entities = (req.Events ?? new()).Select((e, idx) => new ChapterEvent
        {
            Id = e.Id ?? Guid.NewGuid(),
            StoryProjectId = projectId,
            ChapterId = chapterId,
            Order = e.Order > 0 ? e.Order : idx + 1,
            EventType = e.EventType,
            EventText = e.EventText,
            ActorCharacterIds = e.ActorCharacterIds,
            TargetCharacterIds = e.TargetCharacterIds,
            Location = e.Location,
            TimePoint = e.TimePoint,
            Importance = e.Importance,
            IsIrreversible = e.IsIrreversible,
        }).ToList();

        await _repo.ReplaceForChapterAsync(projectId, chapterId, entities, ct);
        var list = await _repo.GetByChapterAsync(projectId, chapterId, ct);
        return Ok(ApiResponse<List<ChapterEventResponse>>.Ok(list.Select(ToResp).ToList()));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ChapterEventResponse>>> Create(
        Guid projectId, Guid chapterId, [FromBody] UpsertChapterEventRequest req, CancellationToken ct)
    {
        var ev = new ChapterEvent
        {
            StoryProjectId = projectId,
            ChapterId = chapterId,
            Order = req.Order,
            EventType = req.EventType,
            EventText = req.EventText,
            ActorCharacterIds = req.ActorCharacterIds,
            TargetCharacterIds = req.TargetCharacterIds,
            Location = req.Location,
            TimePoint = req.TimePoint,
            Importance = req.Importance,
            IsIrreversible = req.IsIrreversible,
        };
        var saved = await _repo.AddAsync(ev, ct);
        return Ok(ApiResponse<ChapterEventResponse>.Ok(ToResp(saved)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ChapterEventResponse>>> UpdateOne(
        Guid projectId, Guid chapterId, Guid id,
        [FromBody] UpsertChapterEventRequest req, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(projectId, id, ct);
        if (item is null || item.ChapterId != chapterId)
            return NotFound(ApiResponse<ChapterEventResponse>.Fail("事件不存在"));

        item.Order = req.Order;
        item.EventType = req.EventType;
        item.EventText = req.EventText;
        item.ActorCharacterIds = req.ActorCharacterIds;
        item.TargetCharacterIds = req.TargetCharacterIds;
        item.Location = req.Location;
        item.TimePoint = req.TimePoint;
        item.Importance = req.Importance;
        item.IsIrreversible = req.IsIrreversible;

        await _repo.UpdateAsync(item, ct);
        return Ok(ApiResponse<ChapterEventResponse>.Ok(ToResp(item)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        Guid projectId, Guid chapterId, Guid id, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(projectId, id, ct);
        if (item is null || item.ChapterId != chapterId)
            return NotFound(ApiResponse<object>.Fail("事件不存在"));
        await _repo.DeleteAsync(projectId, id, ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    private static ChapterEventResponse ToResp(ChapterEvent e) => new()
    {
        Id = e.Id,
        StoryProjectId = e.StoryProjectId,
        ChapterId = e.ChapterId,
        Order = e.Order,
        EventType = e.EventType,
        EventText = e.EventText,
        ActorCharacterIds = e.ActorCharacterIds,
        TargetCharacterIds = e.TargetCharacterIds,
        Location = e.Location,
        TimePoint = e.TimePoint,
        Importance = e.Importance,
        IsIrreversible = e.IsIrreversible,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
    };
}
