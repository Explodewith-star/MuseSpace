using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Services.Story;
using MuseSpace.Contracts.Characters;
using MuseSpace.Contracts.Common;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/characters")]
public class CharactersController : ControllerBase
{
    private readonly CharacterAppService _service;

    public CharactersController(CharacterAppService service)
        => _service = service;

    /// <summary>
    /// 创建角色卡
    /// </summary>
    /// <param name="projectId"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<CharacterResponse>>> Create(
        Guid projectId, [FromBody] CreateCharacterRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(projectId, request, cancellationToken);
        return Ok(ApiResponse<CharacterResponse>.Ok(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<CharacterResponse>>>> GetAll(
        Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByProjectAsync(projectId, cancellationToken);
        return Ok(ApiResponse<List<CharacterResponse>>.Ok(result));
    }

    [HttpGet("{characterId:guid}")]
    public async Task<ActionResult<ApiResponse<CharacterResponse>>> GetById(
        Guid projectId, Guid characterId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(projectId, characterId, cancellationToken);
        if (result is null) return NotFound(ApiResponse<CharacterResponse>.Fail("Character not found"));
        return Ok(ApiResponse<CharacterResponse>.Ok(result));
    }

    [HttpDelete("{characterId:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid projectId, Guid characterId, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(projectId, characterId, cancellationToken);
        if (!deleted) return NotFound(ApiResponse<bool>.Fail("Character not found"));
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
