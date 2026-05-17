using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Services.Story;
using MuseSpace.Contracts.Characters;
using MuseSpace.Contracts.Common;

namespace MuseSpace.Api.Controllers;

/// <summary>
/// 角色池：storyOutlineId IS NULL 的角色集合。
/// 提供项目级池管理和跨项目全局视图。
/// </summary>
[ApiController]
public class CharacterPoolController : ControllerBase
{
    private readonly CharacterAppService _service;
    private readonly StoryProjectAppService _projectService;

    public CharacterPoolController(CharacterAppService service, StoryProjectAppService projectService)
    {
        _service = service;
        _projectService = projectService;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── 全局角色池（跨项目聚合） ────────────────────────────────────────────

    /// <summary>
    /// 获取当前用户所有项目的角色池（全局视图）。
    /// </summary>
    [HttpGet("api/character-pool")]
    public async Task<ActionResult<ApiResponse<List<CharacterResponse>>>> GetGlobalPool(CancellationToken cancellationToken)
    {
        var projects = await _projectService.GetByUserIdAsync(CurrentUserId, cancellationToken);
        var projectIds = projects.Select(p => p.Id);
        var result = await _service.GetGlobalPoolAsync(projectIds, cancellationToken);
        return Ok(ApiResponse<List<CharacterResponse>>.Ok(result));
    }

    // ── 项目级角色池 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 获取指定项目的角色池。
    /// </summary>
    [HttpGet("api/projects/{projectId:guid}/character-pool")]
    public async Task<ActionResult<ApiResponse<List<CharacterResponse>>>> GetPool(
        Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _service.GetPoolAsync(projectId, cancellationToken);
        return Ok(ApiResponse<List<CharacterResponse>>.Ok(result));
    }

    /// <summary>
    /// 在项目角色池中直接新建角色（storyOutlineId = null）。
    /// </summary>
    [HttpPost("api/projects/{projectId:guid}/character-pool")]
    public async Task<ActionResult<ApiResponse<CharacterResponse>>> CreateInPool(
        Guid projectId, [FromBody] CreateCharacterRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateInPoolAsync(projectId, request, cancellationToken);
        return Ok(ApiResponse<CharacterResponse>.Ok(result));
    }

    /// <summary>
    /// 将角色池中的角色引入到指定大纲（隔离复制，原著池保持不变）。
    /// </summary>
    [HttpPost("api/projects/{projectId:guid}/character-pool/import-to-outline/{outlineId:guid}")]
    public async Task<ActionResult<ApiResponse<List<CharacterResponse>>>> ImportToOutline(
        Guid projectId, Guid outlineId, [FromBody] ImportFromPoolRequest request, CancellationToken cancellationToken)
    {
        if (request.CharacterIds is null || request.CharacterIds.Count == 0)
            return BadRequest(ApiResponse<List<CharacterResponse>>.Fail("请选择要引入的角色"));

        var result = await _service.ImportFromPoolAsync(projectId, outlineId, request.CharacterIds, cancellationToken);
        return Ok(ApiResponse<List<CharacterResponse>>.Ok(result));
    }

    /// <summary>
    /// 批量删除角色池中的角色（支持多选全选删除）。
    /// </summary>
    [HttpDelete("api/projects/{projectId:guid}/character-pool")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteMany(
        Guid projectId, [FromBody] DeletePoolCharactersRequest request, CancellationToken cancellationToken)
    {
        if (request.CharacterIds is null || request.CharacterIds.Count == 0)
            return BadRequest(ApiResponse<bool>.Fail("请选择要删除的角色"));

        await _service.DeleteManyFromPoolAsync(projectId, request.CharacterIds, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    /// <summary>
    /// 将全局池中指定角色复制一份到目标项目的角色池。
    /// </summary>
    [HttpPost("api/projects/{projectId:guid}/character-pool/copy-from-global")]
    public async Task<ActionResult<ApiResponse<List<CharacterResponse>>>> CopyFromGlobal(
        Guid projectId, [FromBody] ImportFromPoolRequest request, CancellationToken cancellationToken)
    {
        if (request.CharacterIds is null || request.CharacterIds.Count == 0)
            return BadRequest(ApiResponse<List<CharacterResponse>>.Fail("请选择要复制的角色"));

        // 获取当前用户全局池，找到指定角色
        var userProjects = await _projectService.GetByUserIdAsync(CurrentUserId, cancellationToken);
        var projectIds = userProjects.Select(p => p.Id);
        var globalPool = await _service.GetGlobalPoolAsync(projectIds, cancellationToken);

        var toCreate = globalPool
            .Where(c => request.CharacterIds.Contains(c.Id))
            .ToList();

        var results = new List<CharacterResponse>();
        foreach (var src in toCreate)
        {
            var created = await _service.CreateInPoolAsync(projectId, new CreateCharacterRequest
            {
                Name = src.Name,
                Age = src.Age,
                Role = src.Role,
                PersonalitySummary = src.PersonalitySummary,
                Motivation = src.Motivation,
                SpeakingStyle = src.SpeakingStyle,
                ForbiddenBehaviors = src.ForbiddenBehaviors,
                CurrentState = src.CurrentState,
                Tags = src.Tags,
            }, cancellationToken);
            results.Add(created);
        }

        return Ok(ApiResponse<List<CharacterResponse>>.Ok(results));
    }
}
