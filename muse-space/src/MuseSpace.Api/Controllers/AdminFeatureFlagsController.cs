using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Abstractions.Features;
using MuseSpace.Contracts.Common;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/feature-flags")]
public class AdminFeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagService _service;
    public AdminFeatureFlagsController(IFeatureFlagService service) => _service = service;

    public sealed class UpsertFlagRequest
    {
        public string Key { get; set; } = "";
        public bool IsEnabled { get; set; }
        public string? Description { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<FeatureFlag>>>> List(CancellationToken ct)
        => Ok(ApiResponse<List<FeatureFlag>>.Ok(await _service.ListAsync(ct)));

    [HttpPut]
    public async Task<ActionResult<ApiResponse<object>>> Upsert(
        [FromBody] UpsertFlagRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Key))
            return BadRequest(ApiResponse<object>.Fail("key 不能为空"));
        await _service.UpsertAsync(req.Key, req.IsEnabled, req.Description, ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
