using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Services.Drafting;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.Draft;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/draft")]
public class DraftController : ControllerBase
{
    private readonly GenerateSceneDraftAppService _sceneDraftService;

    public DraftController(GenerateSceneDraftAppService sceneDraftService)
    {
        _sceneDraftService = sceneDraftService;
    }

    [HttpPost("scene")]
    public async Task<ActionResult<ApiResponse<GenerateSceneDraftResponse>>> GenerateSceneDraft(
        [FromBody] GenerateSceneDraftRequest request, 
        CancellationToken cancellationToken)
    {
        var result = await _sceneDraftService.ExecuteAsync(request, cancellationToken);
        return Ok(ApiResponse<GenerateSceneDraftResponse>.Ok(result, result.RequestId));
    }
}
