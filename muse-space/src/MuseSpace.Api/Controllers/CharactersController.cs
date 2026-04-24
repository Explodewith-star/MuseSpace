using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Abstractions.Agents;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Memory;
using MuseSpace.Application.Services.Agents;
using MuseSpace.Application.Services.Story;
using MuseSpace.Contracts.Characters;
using MuseSpace.Contracts.Common;
using System.Security.Claims;
using System.Text.Json;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/characters")]
public class CharactersController : ControllerBase
{
    private readonly CharacterAppService _service;
    private readonly INovelMemorySearchService _novelSearch;
    private readonly IAgentRunner _agentRunner;

    public CharactersController(
        CharacterAppService service,
        INovelMemorySearchService novelSearch,
        IAgentRunner agentRunner)
    {
        _service = service;
        _novelSearch = novelSearch;
        _agentRunner = agentRunner;
    }

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

    [HttpPut("{characterId:guid}")]
    public async Task<ActionResult<ApiResponse<CharacterResponse>>> Update(
        Guid projectId, Guid characterId, [FromBody] UpdateCharacterRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(projectId, characterId, request, cancellationToken);
        if (result is null) return NotFound(ApiResponse<CharacterResponse>.Fail("Character not found"));
        return Ok(ApiResponse<CharacterResponse>.Ok(result));
    }

    /// <summary>
    /// 从原著向量库中 AI 提取角色信息（仅返回建议值，不自动保存）
    /// </summary>
    [HttpPost("extract-from-novel")]
    public async Task<ActionResult<ApiResponse<ExtractCharacterResponse>>> ExtractFromNovel(
        Guid projectId, [FromBody] ExtractCharacterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(ApiResponse<ExtractCharacterResponse>.Fail("请描述要提取的角色"));

        // 1. 向量检索原著相关片段（topK=10，扩大上下文覆盖）
        var chunks = await _novelSearch.SearchAsync(projectId, request.Query, topK: 10, ct: cancellationToken);
        var relevant = chunks.Where(c => c.Similarity > 0.25).ToList();

        if (relevant.Count == 0)
            return BadRequest(ApiResponse<ExtractCharacterResponse>.Fail("未在原著中找到相关内容，请确认已完成导入和向量化"));

        var novelContext = string.Join("\n\n---\n\n",
            relevant.Select((c, i) => $"[片段{i + 1}]\n{c.Content}"));

        // 2. 通过 AgentRunner 调用角色提取 Agent
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var agentContext = new AgentRunContext
        {
            UserId = userId is not null ? Guid.Parse(userId) : null,
            ProjectId = projectId,
        };

        var userPrompt = $"请从以下原著片段中提取角色「{request.Query}」的信息：\n\n{novelContext}";
        var agentResult = await _agentRunner.RunAsync(
            CharacterExtractAgentDefinition.AgentName,
            userPrompt,
            agentContext,
            cancellationToken);

        if (!agentResult.Success)
            return StatusCode(502, ApiResponse<ExtractCharacterResponse>.Fail(
                agentResult.ErrorMessage ?? "AI 提取失败，请重试"));

        // 3. 解析 JSON（宽容处理 markdown 代码块包装）
        var json = agentResult.Output.Trim();
        if (json.StartsWith("```"))
            json = System.Text.RegularExpressions.Regex.Replace(json, @"```\w*\n?", "").Trim('`').Trim();

        ExtractCharacterResponse extracted;
        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<ExtractCharacterResponse>(json, opts)
                        ?? throw new InvalidOperationException("Empty result");
            extracted = new ExtractCharacterResponse
            {
                Name = parsed.Name,
                Age = parsed.Age,
                Role = parsed.Role,
                PersonalitySummary = parsed.PersonalitySummary,
                Motivation = parsed.Motivation,
                SpeakingStyle = parsed.SpeakingStyle,
                ForbiddenBehaviors = parsed.ForbiddenBehaviors,
                CurrentState = parsed.CurrentState,
                SourceChunkCount = relevant.Count,
            };
        }
        catch (Exception)
        {
            return StatusCode(502, ApiResponse<ExtractCharacterResponse>.Fail("AI 返回格式异常，请重试"));
        }

        return Ok(ApiResponse<ExtractCharacterResponse>.Ok(extracted));
    }
}
