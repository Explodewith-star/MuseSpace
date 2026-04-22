using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Contracts.Common;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/llm-provider")]
public class LlmProviderController : ControllerBase
{
    private readonly LlmProviderSelector _selector;

    public LlmProviderController(LlmProviderSelector selector)
        => _selector = selector;

    /// <summary>获取当前激活的 LLM 渠道</summary>
    [HttpGet]
    public ActionResult<ApiResponse<LlmProviderStatusResponse>> Get()
        => Ok(ApiResponse<LlmProviderStatusResponse>.Ok(new LlmProviderStatusResponse(_selector.Active)));

    /// <summary>切换 LLM 渠道（openrouter / deepseek）</summary>
    [HttpPut]
    public ActionResult<ApiResponse<LlmProviderStatusResponse>> Set(
        [FromBody] SetLlmProviderRequest request)
    {
        if (!Enum.TryParse<LlmProviderType>(request.Provider, ignoreCase: true, out var provider))
            return BadRequest(ApiResponse<LlmProviderStatusResponse>.Fail(
                $"未知渠道 '{request.Provider}'，可选值：OpenRouter, DeepSeek"));

        _selector.Active = provider;
        return Ok(ApiResponse<LlmProviderStatusResponse>.Ok(new LlmProviderStatusResponse(provider)));
    }
}

public sealed record LlmProviderStatusResponse(LlmProviderType Active);

public sealed record SetLlmProviderRequest(string Provider);
