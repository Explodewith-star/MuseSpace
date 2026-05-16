using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Abstractions.Agents;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Memory;
using MuseSpace.Application.Services.Agents;
using MuseSpace.Application.Services.Story;
using MuseSpace.Application.Services.Suggestions;
using MuseSpace.Contracts.Characters;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.Suggestions;
using System.Security.Claims;
using System.Text.Json;

namespace MuseSpace.Api.Controllers;

/// <summary>
/// 按大纲管理角色（角色归属大纲隔离，互不污染）。
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/outlines/{outlineId:guid}/characters")]
public class CharactersController : ControllerBase
{
    private readonly CharacterAppService _service;
    private readonly INovelMemorySearchService _novelSearch;
    private readonly IAgentRunner _agentRunner;
    private readonly AgentSuggestionAppService _suggestionService;

    public CharactersController(
        CharacterAppService service,
        INovelMemorySearchService novelSearch,
        IAgentRunner agentRunner,
        AgentSuggestionAppService suggestionService)
    {
        _service = service;
        _novelSearch = novelSearch;
        _agentRunner = agentRunner;
        _suggestionService = suggestionService;
    }

    /// <summary>
    /// 创建角色卡（归属当前大纲）
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<CharacterResponse>>> Create(
        Guid projectId, Guid outlineId, [FromBody] CreateCharacterRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(projectId, outlineId, request, cancellationToken);
        return Ok(ApiResponse<CharacterResponse>.Ok(result));
    }

    /// <summary>
    /// 获取当前大纲下的角色列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<CharacterResponse>>>> GetByOutline(
        Guid projectId, Guid outlineId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByOutlineAsync(outlineId, cancellationToken);
        return Ok(ApiResponse<List<CharacterResponse>>.Ok(result));
    }

    [HttpGet("{characterId:guid}")]
    public async Task<ActionResult<ApiResponse<CharacterResponse>>> GetById(
        Guid projectId, Guid outlineId, Guid characterId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(projectId, characterId, cancellationToken);
        if (result is null) return NotFound(ApiResponse<CharacterResponse>.Fail("Character not found"));
        return Ok(ApiResponse<CharacterResponse>.Ok(result));
    }

    [HttpDelete("{characterId:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid projectId, Guid outlineId, Guid characterId, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(projectId, characterId, cancellationToken);
        if (!deleted) return NotFound(ApiResponse<bool>.Fail("Character not found"));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPut("{characterId:guid}")]
    public async Task<ActionResult<ApiResponse<CharacterResponse>>> Update(
        Guid projectId, Guid outlineId, Guid characterId, [FromBody] UpdateCharacterRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(projectId, characterId, request, cancellationToken);
        if (result is null) return NotFound(ApiResponse<CharacterResponse>.Fail("Character not found"));
        return Ok(ApiResponse<CharacterResponse>.Ok(result));
    }

    /// <summary>
    /// 从其他大纲复制角色到目标大纲（隔离复制，各自独立演化）
    /// </summary>
    [HttpPost("copy")]
    public async Task<ActionResult<ApiResponse<List<CharacterResponse>>>> CopyToOutline(
        Guid projectId, Guid outlineId, [FromBody] CopyCharactersRequest request, CancellationToken cancellationToken)
    {
        // 确保目标大纲与路由一致
        var req = new CopyCharactersRequest { CharacterIds = request.CharacterIds, TargetOutlineId = outlineId };
        var result = await _service.CopyToOutlineAsync(projectId, req, cancellationToken);
        return Ok(ApiResponse<List<CharacterResponse>>.Ok(result));
    }

    /// <summary>
    /// AI 生成角色信息（支持从原著提取或自由生成）。
    /// - FromNovel=true：向量检索原著 + 提取 Agent
    /// - FromNovel=false：纯 AI 生成 Agent
    /// 仅返回建议值，不自动保存。
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult<ApiResponse<ExtractCharacterResponse>>> GenerateCharacter(
        Guid projectId, Guid outlineId, [FromBody] GenerateCharacterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(ApiResponse<ExtractCharacterResponse>.Fail("请描述角色的基本信息"));

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var agentContext = new AgentRunContext
        {
            UserId = userId is not null ? Guid.Parse(userId) : null,
            ProjectId = projectId,
        };

        string userPrompt;
        string agentName;
        int sourceChunkCount = 0;

        if (request.FromNovel)
        {
            // 从原著向量库检索相关片段
            var chunks = await _novelSearch.SearchAsync(projectId, request.Description, topK: 10, ct: cancellationToken);
            var relevant = chunks.Where(c => c.Similarity > 0.25).ToList();

            if (relevant.Count == 0)
                return BadRequest(ApiResponse<ExtractCharacterResponse>.Fail("未在原著中找到相关内容，请确认已完成导入和向量化"));

            var novelContext = string.Join("\n\n---\n\n",
                relevant.Select((c, i) => $"[片段{i + 1}]\n{c.Content}"));

            userPrompt = $"请从以下原著片段中提取角色「{request.Description}」的信息：\n\n{novelContext}";
            agentName = CharacterExtractAgentDefinition.AgentName;
            sourceChunkCount = relevant.Count;
        }
        else
        {
            userPrompt = $"请根据以下描述生成角色信息：\n\n{request.Description}";
            agentName = CharacterGenerationAgentDefinition.AgentName;
        }

        var agentResult = await _agentRunner.RunAsync(agentName, userPrompt, agentContext, cancellationToken);

        if (!agentResult.Success)
            return StatusCode(502, ApiResponse<ExtractCharacterResponse>.Fail(
                agentResult.ErrorMessage ?? "AI 生成失败，请重试"));

        // 解析 JSON（宽容处理 markdown 代码块包装）
        var json = agentResult.Output.Trim();
        if (json.StartsWith("```"))
            json = System.Text.RegularExpressions.Regex.Replace(json, @"```\w*\n?", "").Trim('`').Trim();

        ExtractCharacterResponse generated;
        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<ExtractCharacterResponse>(json, opts)
                        ?? throw new InvalidOperationException("Empty result");
            generated = new ExtractCharacterResponse
            {
                Name = parsed.Name,
                Age = parsed.Age,
                Role = parsed.Role,
                PersonalitySummary = parsed.PersonalitySummary,
                Motivation = parsed.Motivation,
                SpeakingStyle = parsed.SpeakingStyle,
                ForbiddenBehaviors = parsed.ForbiddenBehaviors,
                CurrentState = parsed.CurrentState,
                SourceChunkCount = sourceChunkCount,
            };
        }
        catch
        {
            return StatusCode(502, ApiResponse<ExtractCharacterResponse>.Fail("AI 返回格式异常，请重试"));
        }

        return Ok(ApiResponse<ExtractCharacterResponse>.Ok(generated));
    }
}
