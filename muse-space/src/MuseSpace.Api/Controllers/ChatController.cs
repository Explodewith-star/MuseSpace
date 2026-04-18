using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.Llm;

namespace MuseSpace.Api.Controllers;

/// <summary>
/// 简单的模型问答接口，用于测试模型边界。
/// 直接调用 LLM 而不经过完整的 Skill 编排流程。
/// </summary>
[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly ILlmClient _llmClient;

    public ChatController(ILlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    /// <summary>
    /// 发送问题给模型并获取回答。
    /// </summary>
    /// <param name="request">包含用户问题和可选的系统角色设定</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型的回答和耗时统计</returns>
    [HttpPost("ask")]
    public async Task<ActionResult<ApiResponse<ChatResponse>>> Ask(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var systemPrompt = request.SystemPrompt ?? "You are a helpful assistant.";
        var answer = await _llmClient.ChatAsync(systemPrompt, request.Question, cancellationToken);
        
        stopwatch.Stop();
        
        var response = new ChatResponse
        {
            Answer = answer,
            DurationMs = stopwatch.ElapsedMilliseconds
        };
        
        return Ok(ApiResponse<ChatResponse>.Ok(response));
    }
}
