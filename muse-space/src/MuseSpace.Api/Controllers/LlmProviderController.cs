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
    private readonly VeniceOptions _veniceOptions;
    private readonly MuseSpaceDbContext _db;

    public LlmProviderController(
        LlmProviderSelector selector,
        IOptions<LlmOptions> llmOptions,
        IOptions<VeniceOptions> veniceOptions,
        MuseSpaceDbContext db)
    {
        _selector = selector;
        _llmOptions = llmOptions.Value;
        _veniceOptions = veniceOptions.Value;
        _db = db;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private bool IsAdminRequest => User.IsInRole("Admin");

    /// <summary>
    /// 按当前激活渠道返回对应的模型列表。
    /// DeepSeek 无多模型，返回空列表。
    /// </summary>
    private IReadOnlyList<ModelOption> ResolveAvailableModels() => _selector.Active switch
    {
        LlmProviderType.OpenRouter => _llmOptions.AvailableModels.Count > 0
            ? _llmOptions.AvailableModels
            : [new ModelOption { Id = _llmOptions.ModelName, Label = _llmOptions.ModelName }],
        LlmProviderType.Venice => _veniceOptions.AvailableModels.Count > 0
            ? _veniceOptions.AvailableModels
            : [new ModelOption { Id = _veniceOptions.ModelName, Label = _veniceOptions.ModelName }],
        _ => [],
    };

    /// <summary>当前渠道下用于展示的模型名称。</summary>
    private string CurrentModelDisplay() => _selector.Active switch
    {
        LlmProviderType.Venice => _selector.ActiveModel ?? _veniceOptions.ModelName,
        _ => _selector.ActiveModel ?? _llmOptions.ModelName,
    };

    /// <summary>
    /// 若存储的 ActiveModel 不在当前渠道白名单内，返回 null 回退到默认值。
    /// </summary>
    private string? SanitizeModel(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return null;
        var models = ResolveAvailableModels();
        if (models.Count == 0) return null;
        return models.Any(m => m.Id == modelId) ? modelId : null;
    }

    /// <summary>
    /// 根据 modelId 自动反查所属渠道及清洗后的 ID。
    /// 先查 OpenRouter 白名单，再查 Venice 白名单。
    /// 两边都找不到时保持当前渠道，modelId 置 null（回退默认）。
    /// </summary>
    private (LlmProviderType Provider, string? ModelId) ResolveProviderAndModel(string modelId)
    {
        if (_llmOptions.AvailableModels.Count > 0 && _llmOptions.AvailableModels.Any(m => m.Id == modelId))
            return (LlmProviderType.OpenRouter, modelId);

        if (_veniceOptions.AvailableModels.Count > 0 && _veniceOptions.AvailableModels.Any(m => m.Id == modelId))
            return (LlmProviderType.Venice, modelId);

        // 两张白名单均未配置时（开发模式），交由当前激活渠道处理
        if (_llmOptions.AvailableModels.Count == 0 && _veniceOptions.AvailableModels.Count == 0)
            return (_selector.Active, modelId);

        // 找到了白名单但 modelId 不在其中 → 回退
        return (_selector.Active, null);
    }

    /// <summary>获取当前状态：激活渠道、当前模型、可选模型列表。Venice 仅 Admin 可见。</summary>
    [HttpGet]
    public ActionResult<ApiResponse<LlmProviderStatusResponse>> Get()
    {
        // 非管理员持有 Venice 偏好时（理论上不应发生，作为防御层）回落到 DeepSeek
        if (_selector.Active == LlmProviderType.Venice && !IsAdminRequest)
        {
            _selector.Active = LlmProviderType.DeepSeek;
            _selector.ActiveModel = null;
        }

        _selector.ActiveModel = SanitizeModel(_selector.ActiveModel);

        return Ok(ApiResponse<LlmProviderStatusResponse>.Ok(
            new LlmProviderStatusResponse(_selector.Active, CurrentModelDisplay(), ResolveAvailableModels())));
    }

    /// <summary>切换渠道并持久化。Venice 仅 Admin 可调用。</summary>
    [HttpPut]
    public async Task<ActionResult<ApiResponse<LlmProviderStatusResponse>>> Set(
        [FromBody] SetLlmProviderRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<LlmProviderType>(request.Provider, ignoreCase: true, out var provider))
            return BadRequest(ApiResponse<LlmProviderStatusResponse>.Fail(
                $"未知渠道 '{request.Provider}'，可选值：OpenRouter, DeepSeek, Venice"));

        if (provider == LlmProviderType.Venice && !IsAdminRequest)
            return Forbid();

        _selector.Active = provider;

        // 切换到 DeepSeek 时清除模型选择；切换到 Venice 时默认第一个可用模型
        _selector.ActiveModel = provider switch
        {
            LlmProviderType.DeepSeek => null,
            LlmProviderType.Venice   => _veniceOptions.AvailableModels.FirstOrDefault()?.Id ?? _veniceOptions.ModelName,
            _                        => _selector.ActiveModel, // OpenRouter 保留原模型
        };

        await PersistAsync(ct);

        return Ok(ApiResponse<LlmProviderStatusResponse>.Ok(
            new LlmProviderStatusResponse(_selector.Active, CurrentModelDisplay(), ResolveAvailableModels())));
    }

    /// <summary>
    /// 切换模型并持久化。自动反查所属渠道；若目标渠道为 Venice 则要求 Admin。
    /// </summary>
    [HttpPut("model")]
    public async Task<ActionResult<ApiResponse<LlmProviderStatusResponse>>> SetModel(
        [FromBody] SetLlmModelRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ModelId))
            return BadRequest(ApiResponse<LlmProviderStatusResponse>.Fail("模型 ID 不能为空"));

        var (provider, sanitized) = ResolveProviderAndModel(request.ModelId);

        if (provider == LlmProviderType.Venice && !IsAdminRequest)
            return Forbid();

        _selector.Active = provider;
        _selector.ActiveModel = sanitized;

        await PersistAsync(ct);

        return Ok(ApiResponse<LlmProviderStatusResponse>.Ok(
            new LlmProviderStatusResponse(_selector.Active, CurrentModelDisplay(), ResolveAvailableModels())));
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

