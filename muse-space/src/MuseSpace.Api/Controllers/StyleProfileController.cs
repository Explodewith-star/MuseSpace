using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Services.Story;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.StyleProfiles;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/style-profile")]
public class StyleProfileController : ControllerBase
{
    private readonly StyleProfileAppService _service;

    public StyleProfileController(StyleProfileAppService service)
        => _service = service;

    [HttpPut]
    public async Task<ActionResult<ApiResponse<StyleProfileResponse>>> Upsert(
        Guid projectId, [FromBody] UpsertStyleProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpsertAsync(projectId, request, cancellationToken);
        return Ok(ApiResponse<StyleProfileResponse>.Ok(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<StyleProfileResponse>>> Get(
        Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByProjectAsync(projectId, cancellationToken);
        if (result is null) return NotFound(ApiResponse<StyleProfileResponse>.Fail("Style profile not found"));
        return Ok(ApiResponse<StyleProfileResponse>.Ok(result));
    }
}
