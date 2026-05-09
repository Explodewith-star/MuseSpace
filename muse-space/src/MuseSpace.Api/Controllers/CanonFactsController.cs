using Microsoft.AspNetCore.Mvc;
using MuseSpace.Api.Authorization;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Contracts.CanonFacts;
using MuseSpace.Contracts.Common;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Api.Controllers;

/// <summary>
/// Module D 正典事实层：固定事实（Canon Fact）CRUD。
/// </summary>
[ApiController]
[ProjectAccess]
[Route("api/projects/{projectId:guid}/canon-facts")]
public class CanonFactsController : ControllerBase
{
    private readonly ICanonFactRepository _repo;
    public CanonFactsController(ICanonFactRepository repo) => _repo = repo;

    /// <summary>查询项目事实，支持 ?onlyActive=true 或 ?onlyLocked=true。</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<CanonFactResponse>>>> GetAll(
        Guid projectId,
        [FromQuery] Guid? storyOutlineId,
        [FromQuery] bool onlyActive = false,
        [FromQuery] bool onlyLocked = false,
        CancellationToken ct = default)
    {
        var list = storyOutlineId.HasValue
            ? onlyLocked
                ? await _repo.GetLockedByOutlineAsync(projectId, storyOutlineId.Value, ct)
                : onlyActive
                    ? await _repo.GetActiveByOutlineAsync(projectId, storyOutlineId.Value, ct)
                    : await _repo.GetByOutlineAsync(projectId, storyOutlineId.Value, ct)
            : onlyLocked
                ? await _repo.GetLockedAsync(projectId, ct)
                : onlyActive
                    ? await _repo.GetActiveAsync(projectId, ct)
                    : await _repo.GetByProjectAsync(projectId, ct);
        return Ok(ApiResponse<List<CanonFactResponse>>.Ok(list.Select(ToResp).ToList()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CanonFactResponse>>> GetOne(
        Guid projectId, Guid id, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(projectId, id, ct);
        return item is null
            ? NotFound(ApiResponse<CanonFactResponse>.Fail("事实不存在"))
            : Ok(ApiResponse<CanonFactResponse>.Ok(ToResp(item)));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CanonFactResponse>>> Create(
        Guid projectId, [FromBody] UpsertCanonFactRequest req, CancellationToken ct)
    {
        var fact = new CanonFact
        {
            StoryProjectId = projectId,
            StoryOutlineId = req.StoryOutlineId ?? Guid.Empty,
            FactType = req.FactType,
            SubjectId = req.SubjectId,
            ObjectId = req.ObjectId,
            FactKey = req.FactKey,
            FactValue = req.FactValue,
            SourceChapterId = req.SourceChapterId,
            Confidence = req.Confidence ?? 1.0,
            IsLocked = req.IsLocked ?? false,
            InvalidatedByChapterId = req.InvalidatedByChapterId,
            Notes = req.Notes,
        };
        var saved = await _repo.AddAsync(fact, ct);
        return Ok(ApiResponse<CanonFactResponse>.Ok(ToResp(saved)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CanonFactResponse>>> Update(
        Guid projectId, Guid id, [FromBody] UpsertCanonFactRequest req, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(projectId, id, ct);
        if (item is null) return NotFound(ApiResponse<CanonFactResponse>.Fail("事实不存在"));

        item.FactType = req.FactType;
        if (req.StoryOutlineId.HasValue)
            item.StoryOutlineId = req.StoryOutlineId.Value;
        item.SubjectId = req.SubjectId;
        item.ObjectId = req.ObjectId;
        item.FactKey = req.FactKey;
        item.FactValue = req.FactValue;
        item.SourceChapterId = req.SourceChapterId;
        item.Confidence = req.Confidence ?? item.Confidence;
        item.IsLocked = req.IsLocked ?? item.IsLocked;
        item.InvalidatedByChapterId = req.InvalidatedByChapterId;
        item.Notes = req.Notes;

        await _repo.UpdateAsync(item, ct);
        return Ok(ApiResponse<CanonFactResponse>.Ok(ToResp(item)));
    }

    /// <summary>锁定 / 解锁 / 修正文案。</summary>
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CanonFactResponse>>> Patch(
        Guid projectId, Guid id, [FromBody] PatchCanonFactRequest req, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(projectId, id, ct);
        if (item is null) return NotFound(ApiResponse<CanonFactResponse>.Fail("事实不存在"));

        if (req.FactValue is not null) item.FactValue = req.FactValue;
        if (req.IsLocked is not null) item.IsLocked = req.IsLocked.Value;
        if (req.InvalidatedByChapterId is not null) item.InvalidatedByChapterId = req.InvalidatedByChapterId;
        if (req.Notes is not null) item.Notes = req.Notes;

        await _repo.UpdateAsync(item, ct);
        return Ok(ApiResponse<CanonFactResponse>.Ok(ToResp(item)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        Guid projectId, Guid id, CancellationToken ct)
    {
        await _repo.DeleteAsync(projectId, id, ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    private static CanonFactResponse ToResp(CanonFact f) => new()
    {
        Id = f.Id,
        StoryProjectId = f.StoryProjectId,
        StoryOutlineId = f.StoryOutlineId,
        FactType = f.FactType,
        SubjectId = f.SubjectId,
        ObjectId = f.ObjectId,
        FactKey = f.FactKey,
        FactValue = f.FactValue,
        SourceChapterId = f.SourceChapterId,
        Confidence = f.Confidence,
        IsLocked = f.IsLocked,
        InvalidatedByChapterId = f.InvalidatedByChapterId,
        Notes = f.Notes,
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt,
    };
}
