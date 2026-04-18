using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Services.Story;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.WorldRules;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/world-rules")]
public class WorldRulesController : ControllerBase
{
    private readonly WorldRuleAppService _service;

    public WorldRulesController(WorldRuleAppService service)
        => _service = service;

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WorldRuleResponse>>> Create(
        Guid projectId, [FromBody] CreateWorldRuleRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(projectId, request, cancellationToken);
        return Ok(ApiResponse<WorldRuleResponse>.Ok(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<WorldRuleResponse>>>> GetAll(
        Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByProjectAsync(projectId, cancellationToken);
        return Ok(ApiResponse<List<WorldRuleResponse>>.Ok(result));
    }

    [HttpGet("{ruleId:guid}")]
    public async Task<ActionResult<ApiResponse<WorldRuleResponse>>> GetById(
        Guid projectId, Guid ruleId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(projectId, ruleId, cancellationToken);
        if (result is null) return NotFound(ApiResponse<WorldRuleResponse>.Fail("World rule not found"));
        return Ok(ApiResponse<WorldRuleResponse>.Ok(result));
    }

    [HttpDelete("{ruleId:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid projectId, Guid ruleId, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(projectId, ruleId, cancellationToken);
        if (!deleted) return NotFound(ApiResponse<bool>.Fail("World rule not found"));
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
