using System.Security.Claims;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Services.Suggestions;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Domain.Enums;
using MuseSpace.Infrastructure.Jobs;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/suggestions")]
public class AgentSuggestionsController : ControllerBase
{
    private readonly AgentSuggestionAppService _service;
    private readonly IBackgroundJobClient _backgroundJobs;

    public AgentSuggestionsController(AgentSuggestionAppService service, IBackgroundJobClient backgroundJobs)
    {
        _service = service;
        _backgroundJobs = backgroundJobs;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>获取项目建议列表，可按 category 和 status 筛选。</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<AgentSuggestionResponse>>>> GetAll(
        Guid projectId, [FromQuery] string? category, [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        SuggestionStatus? statusEnum = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<SuggestionStatus>(status, true, out var parsed))
            statusEnum = parsed;

        var result = await _service.GetByProjectAsync(projectId, category, statusEnum, cancellationToken);
        return Ok(ApiResponse<List<AgentSuggestionResponse>>.Ok(result));
    }

    /// <summary>获取单条建议详情。</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<AgentSuggestionResponse>>> GetById(
        Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result is null || result.StoryProjectId != projectId)
            return NotFound(ApiResponse<AgentSuggestionResponse>.Fail("建议不存在"));
        return Ok(ApiResponse<AgentSuggestionResponse>.Ok(result));
    }

    /// <summary>接受建议（Pending → Accepted）。</summary>
    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<ApiResponse<AgentSuggestionResponse>>> Accept(
        Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.AcceptAsync(id, cancellationToken);
        if (result is null)
            return BadRequest(ApiResponse<AgentSuggestionResponse>.Fail("建议不存在或状态不可接受"));
        return Ok(ApiResponse<AgentSuggestionResponse>.Ok(result));
    }

    /// <summary>应用建议到正式业务表（Accepted → Applied）。</summary>
    [HttpPost("{id:guid}/apply")]
    public async Task<ActionResult<ApiResponse<AgentSuggestionResponse>>> Apply(
        Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.ApplyAsync(id, cancellationToken);
        if (result is null)
            return BadRequest(ApiResponse<AgentSuggestionResponse>.Fail("建议不存在或尚未接受"));
        return Ok(ApiResponse<AgentSuggestionResponse>.Ok(result));
    }

    /// <summary>忽略建议（Pending → Ignored）。</summary>
    [HttpPost("{id:guid}/ignore")]
    public async Task<ActionResult<ApiResponse<AgentSuggestionResponse>>> Ignore(
        Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.IgnoreAsync(id, cancellationToken);
        if (result is null)
            return BadRequest(ApiResponse<AgentSuggestionResponse>.Fail("建议不存在或状态不可忽略"));
        return Ok(ApiResponse<AgentSuggestionResponse>.Ok(result));
    }

    /// <summary>批量接受或忽略建议。</summary>
    [HttpPost("batch-resolve")]
    public async Task<ActionResult<ApiResponse<int>>> BatchResolve(
        Guid projectId, [FromBody] BatchResolveSuggestionsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Ids.Count == 0)
            return BadRequest(ApiResponse<int>.Fail("请提供建议 ID 列表"));

        if (!request.Action.Equals("Accept", StringComparison.OrdinalIgnoreCase) &&
            !request.Action.Equals("Ignore", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<int>.Fail("Action 仅支持 Accept 或 Ignore"));

        var count = await _service.BatchResolveAsync(request.Ids, request.Action, cancellationToken);
        return Ok(ApiResponse<int>.Ok(count));
    }

    /// <summary>手动触发世界观一致性检查（异步，结果写入建议表）。</summary>
    [HttpPost("consistency-check")]
    public ActionResult<ApiResponse<string>> TriggerConsistencyCheck(
        Guid projectId, [FromBody] ConsistencyCheckRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DraftText))
            return BadRequest(ApiResponse<string>.Fail("草稿文本不能为空"));

        _backgroundJobs.Enqueue<ConsistencyCheckJob>(
            job => job.ExecuteAsync(projectId, request.DraftText, CurrentUserId));

        return Ok(ApiResponse<string>.Ok("一致性检查已提交，结果将异步写入建议列表"));
    }
}
