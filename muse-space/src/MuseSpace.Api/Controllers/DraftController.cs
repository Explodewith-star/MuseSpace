using System.Security.Claims;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Services.Drafting;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.Draft;
using MuseSpace.Infrastructure.Jobs;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/draft")]
public class DraftController : ControllerBase
{
    private readonly GenerateSceneDraftAppService _sceneDraftService;
    private readonly IBackgroundJobClient _backgroundJobs;

    public DraftController(
        GenerateSceneDraftAppService sceneDraftService,
        IBackgroundJobClient backgroundJobs)
    {
        _sceneDraftService = sceneDraftService;
        _backgroundJobs = backgroundJobs;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpPost("scene")]
    public async Task<ActionResult<ApiResponse<GenerateSceneDraftResponse>>> GenerateSceneDraft(
        [FromBody] GenerateSceneDraftRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sceneDraftService.ExecuteAsync(request, cancellationToken);

        // 草稿生成成功后，异步触发世界观一致性检查（不阻塞响应）
        if (!string.IsNullOrEmpty(result.GeneratedText))
        {
            _backgroundJobs.Enqueue<ConsistencyCheckJob>(
                job => job.ExecuteAsync(request.StoryProjectId, result.GeneratedText, CurrentUserId));
        }

        return Ok(ApiResponse<GenerateSceneDraftResponse>.Ok(result, result.RequestId));
    }
}
