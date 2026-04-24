using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Contracts.Common;
using MuseSpace.Domain.Entities;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/llm-provider")]
public class LlmProviderController : ControllerBase
{
    private readonly LlmProviderSelector _selector;
    private readonly LlmOptions _llmOptions;
    private readonly MuseSpaceDbContext _db;

    public LlmProviderController(
        LlmProviderSelector selector,
        IOptions<LlmOptions> llmOptions,
        MuseSpaceDbContext db)
    {
        _selector = selector;
        _llmOptions = llmOptions.Value;
        _db = db;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private IReadOnlyList<ModelOption> ResolveAvailableModels()
        => _llmOptions.AvailableModels.Count > 0
            ? _llmOptions.AvailableModels
            : [new ModelOption { Id = _llmOptions.ModelName, Label = _llmOptions.ModelName }];

    /// <summary>
    /// 若偏好模型不在可选列表，返回 null 以回退到配置默认（避免卡在无效 ID 上）。
    /// </summary>
    private string? SanitizeModel(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return null;
        if (_llmOptions.AvailableModels.Count == 0) return modelId; // 未配置白名单则不干预
        return _llmOptions.AvailableModels.Any(m => m.Id == modelId) ? modelId : null;
    }

    /// <summary>获取当前状态：激活渠道、当前模型、可选模型列表</summary>
    [HttpGet]
    public ActionResult<ApiResponse<LlmProviderStatusResponse>> Get()
    {
        // 中间件已填充，且 SanitizeModel 保证 ActiveModel 要么为 null 要么在白名单内
        var sanitized = SanitizeModel(_selector.ActiveModel);
        if (sanitized != _selector.ActiveModel)
            _selector.ActiveModel = sanitized; // 仅影响本次请求 scope

        var currentModel = _selector.ActiveModel ?? _llmOptions.ModelName;
        return Ok(ApiResponse<LlmProviderStatusResponse>.Ok(
            new LlmProviderStatusResponse(_selector.Active, currentModel, ResolveAvailableModels())));
    }

    /// <summary>切换渠道（OpenRouter / DeepSeek），并持久化到当前用户偏好</summary>
    [HttpPut]
    public async Task<ActionResult<ApiResponse<LlmProviderStatusResponse>>> Set(
        [FromBody] SetLlmProviderRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<LlmProviderType>(request.Provider, ignoreCase: true, out var provider))
            return BadRequest(ApiResponse<LlmProviderStatusResponse>.Fail(
                $"未知渠道 '{request.Provider}'，可选值：OpenRouter, DeepSeek"));

        _selector.Active = provider;
        // 切换到 DeepSeek 时清除 OpenRouter 模型
        if (provider == LlmProviderType.DeepSeek)
            _selector.ActiveModel = null;

        await PersistAsync(ct);

        var current = _selector.ActiveModel ?? _llmOptions.ModelName;
        return Ok(ApiResponse<LlmProviderStatusResponse>.Ok(
            new LlmProviderStatusResponse(provider, current, ResolveAvailableModels())));
    }

    /// <summary>切换 OpenRouter 模型并持久化；传入的模型不在白名单时静默回退默认。</summary>
    [HttpPut("model")]
    public async Task<ActionResult<ApiResponse<LlmProviderStatusResponse>>> SetModel(
        [FromBody] SetLlmModelRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ModelId))
            return BadRequest(ApiResponse<LlmProviderStatusResponse>.Fail("模型 ID 不能为空"));

        _selector.Active = LlmProviderType.OpenRouter;
        _selector.ActiveModel = SanitizeModel(request.ModelId);

        await PersistAsync(ct);

        var current = _selector.ActiveModel ?? _llmOptions.ModelName;
        return Ok(ApiResponse<LlmProviderStatusResponse>.Ok(
            new LlmProviderStatusResponse(LlmProviderType.OpenRouter, current, ResolveAvailableModels())));
    }

    /// <summary>
    /// 将当前 selector 状态写入 user_llm_preferences。
    /// 游客请求（无 userId）只在当前 Scope 生效，不落库。
    /// </summary>
    private async Task PersistAsync(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return;

        var pref = await _db.UserLlmPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId.Value, ct);
        if (pref is null)
        {
            pref = new UserLlmPreference { UserId = userId.Value };
            _db.UserLlmPreferences.Add(pref);
        }
        pref.Provider = _selector.Active.ToString();
        pref.ModelId = _selector.ActiveModel;
        await _db.SaveChangesAsync(ct);
    }
}

public sealed record LlmProviderStatusResponse(
    LlmProviderType Active,
    string CurrentModel,
    IReadOnlyList<ModelOption> AvailableModels);

public sealed record SetLlmProviderRequest(string Provider);

public sealed record SetLlmModelRequest(string ModelId);
