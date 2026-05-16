using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Services.Story;
using MuseSpace.Contracts.Characters;
using MuseSpace.Contracts.Common;

namespace MuseSpace.Api.Controllers;

/// <summary>
/// 原著角色池：项目级只读参考库（StoryOutlineId IS NULL）。
/// 原著导入后自动提取的角色进入此池，用户可按需引入到具体大纲。
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/character-pool")]
public class CharacterPoolController : ControllerBase
{
    private readonly CharacterAppService _service;

    public CharacterPoolController(CharacterAppService service)
        => _service = service;

    /// <summary>
    /// 获取原著角色池（所有 StoryOutlineId 为 null 的角色）。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<CharacterResponse>>>> GetPool(
        Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _service.GetPoolAsync(projectId, cancellationToken);
        return Ok(ApiResponse<List<CharacterResponse>>.Ok(result));
    }

    /// <summary>
    /// 将原著角色池中的角色引入到指定大纲（隔离复制，原著池保持不变）。
    /// 引入后该角色成为大纲独立副本，可自由改写人设。
    /// </summary>
    [HttpPost("import-to-outline/{outlineId:guid}")]
    public async Task<ActionResult<ApiResponse<List<CharacterResponse>>>> ImportToOutline(
        Guid projectId, Guid outlineId, [FromBody] ImportFromPoolRequest request, CancellationToken cancellationToken)
    {
        if (request.CharacterIds is null || request.CharacterIds.Count == 0)
            return BadRequest(ApiResponse<List<CharacterResponse>>.Fail("请选择要引入的角色"));

        var result = await _service.ImportFromPoolAsync(projectId, outlineId, request.CharacterIds, cancellationToken);
        return Ok(ApiResponse<List<CharacterResponse>>.Ok(result));
    }
}
