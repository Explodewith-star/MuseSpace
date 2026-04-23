using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Contracts.Common;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/llm-provider")]
public class LlmProviderController : ControllerBase
{
    private readonly LlmProviderSelector _selector;
    private readonly LlmOptions _llmOptions;

    public LlmProviderController(LlmProviderSelector selector, IOptions<LlmOptions> llmOptions)
    {
        _selector = selector;
        _llmOptions = llmOptions.Value;
    }

    /// <summary>获取当前状态：激活渠道、当前模型、可选模型列表</summary>
    [HttpGet]
    public ActionResult<ApiResponse<LlmProviderStatusResponse>> Get()
    {
        var currentModel = _selector.ActiveModel ?? _llmOptions.ModelName;
        var models = _llmOptions.AvailableModels.Count > 0
            ? _llmOptions.AvailableModels
            : [new ModelOption { Id = _llmOptions.ModelName, Label = _llmOptions.ModelName }];
        return Ok(ApiResponse<LlmProviderStatusResponse>.Ok(
            new LlmProviderStatusResponse(_selector.Active, currentModel, models)));
    }

    /// <summary>切换渠道（openrouter / deepseek）</summary>
    [HttpPut]
    public ActionResult<ApiResponse<LlmProviderStatusResponse>> Set(
        [FromBody] SetLlmProviderRequest request)
    {
        if (!Enum.TryParse<LlmProviderType>(request.Provider, ignoreCase: true, out var provider))
            return BadRequest(ApiResponse<LlmProviderStatusResponse>.Fail(
                $"未知渠道 '{request.Provider}'，可选值：OpenRouter, DeepSeek"));

        _selector.Active = provider;
        // 切换到 DeepSeek 时清除运行时模型
        if (provider == LlmProviderType.DeepSeek)
            _selector.ActiveModel = null;

        return Ok(ApiResponse<LlmProviderStatusResponse>.Ok(
            new LlmProviderStatusResponse(provider, _selector.ActiveModel ?? _llmOptions.ModelName,
                _llmOptions.AvailableModels)));
    }

    /// <summary>切换 OpenRouter 模型（仅 OpenRouter 渠道有效）</summary>
    [HttpPut("model")]
    public ActionResult<ApiResponse<LlmProviderStatusResponse>> SetModel(
        [FromBody] SetLlmModelRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ModelId))
            return BadRequest(ApiResponse<LlmProviderStatusResponse>.Fail("模型 ID 不能为空"));

        _selector.Active = LlmProviderType.OpenRouter;
        _selector.ActiveModel = request.ModelId;

        var models = _llmOptions.AvailableModels.Count > 0
            ? _llmOptions.AvailableModels
            : [new ModelOption { Id = request.ModelId, Label = request.ModelId }];
        return Ok(ApiResponse<LlmProviderStatusResponse>.Ok(
            new LlmProviderStatusResponse(LlmProviderType.OpenRouter, request.ModelId, models)));
    }
}

public sealed record LlmProviderStatusResponse(
    LlmProviderType Active,
    string CurrentModel,
    IReadOnlyList<ModelOption> AvailableModels);

public sealed record SetLlmProviderRequest(string Provider);

public sealed record SetLlmModelRequest(string ModelId);
