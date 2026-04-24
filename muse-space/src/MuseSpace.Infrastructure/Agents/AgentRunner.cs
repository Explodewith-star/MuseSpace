using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Agents;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Domain.Entities;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Infrastructure.Agents;

/// <summary>
/// Agent 运行时默认实现。
///
/// P0 阶段支持：
///   - 按 agentName 查找 AgentDefinition
///   - 无工具 Agent：单次 LLM 调用
///   - AgentRun 持久化（可观测）
///   - Feature Flag 检查
///
/// P1 阶段将扩展：
///   - 工具调用循环（LLM → tool_call → 执行 → 回填 → 重复）
///   - 多步推理与最大步数限制
/// </summary>
public sealed class AgentRunner : IAgentRunner
{
    private readonly ILlmClient _llmClient;
    private readonly MuseSpaceDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgentRunner> _logger;

    private readonly Dictionary<string, AgentDefinition> _definitions;
    private readonly Dictionary<string, IAgentTool> _tools;

    public AgentRunner(
        ILlmClient llmClient,
        MuseSpaceDbContext dbContext,
        IConfiguration configuration,
        ILogger<AgentRunner> logger,
        IEnumerable<AgentDefinition> definitions,
        IEnumerable<IAgentTool> tools)
    {
        _llmClient = llmClient;
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
        _definitions = definitions.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<AgentRunResult> RunAsync(
        string agentName,
        string userInput,
        AgentRunContext context,
        CancellationToken cancellationToken = default)
    {
        // ── Feature Flag 检查 ────────────────────────────────────────────────
        if (!IsEnabled(agentName))
        {
            return new AgentRunResult
            {
                Success = false,
                AgentName = agentName,
                ErrorMessage = $"Agent '{agentName}' is disabled.",
            };
        }

        // ── 查找定义 ────────────────────────────────────────────────────────
        if (!_definitions.TryGetValue(agentName, out var definition))
        {
            return new AgentRunResult
            {
                Success = false,
                AgentName = agentName,
                ErrorMessage = $"No agent registered with name '{agentName}'.",
            };
        }

        // ── 创建 AgentRun 记录 ──────────────────────────────────────────────
        var agentRun = new AgentRun
        {
            Id = context.RunId,
            AgentName = agentName,
            UserId = context.UserId,
            ProjectId = context.ProjectId,
            Status = AgentRunStatus.Running,
            InputPreview = Truncate(userInput, 500),
        };
        _dbContext.AgentRuns.Add(agentRun);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var steps = new List<AgentStep>();

        try
        {
            AgentRunResult result;

            if (definition.ToolNames.Count == 0)
            {
                // ── P0：无工具 Agent，单次 LLM 调用 ────────────────────────
                result = await RunSimpleAsync(definition, userInput, context, steps, cancellationToken);
            }
            else
            {
                // ── P1：工具调用循环（预留） ────────────────────────────────
                result = await RunWithToolsAsync(definition, userInput, context, steps, cancellationToken);
            }

            stopwatch.Stop();

            // 更新运行记录
            agentRun.Status = result.Success ? AgentRunStatus.Succeeded : AgentRunStatus.Failed;
            agentRun.StepCount = steps.Count;
            agentRun.InputTokens = context.TotalInputTokens;
            agentRun.OutputTokens = context.TotalOutputTokens;
            agentRun.DurationMs = stopwatch.ElapsedMilliseconds;
            agentRun.OutputPreview = Truncate(result.Output, 500);
            agentRun.ErrorMessage = result.ErrorMessage;
            agentRun.FinishedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[Agent] {AgentName} RunId={RunId} Steps={Steps} Duration={DurationMs}ms Success={Success}",
                agentName, context.RunId, steps.Count, stopwatch.ElapsedMilliseconds, result.Success);

            return new AgentRunResult
            {
                Success = result.Success,
                Output = result.Output,
                ErrorMessage = result.ErrorMessage,
                AgentName = result.AgentName,
                Steps = result.Steps,
                DurationMs = stopwatch.ElapsedMilliseconds,
                TotalInputTokens = context.TotalInputTokens,
                TotalOutputTokens = context.TotalOutputTokens,
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            agentRun.Status = AgentRunStatus.Failed;
            agentRun.DurationMs = stopwatch.ElapsedMilliseconds;
            agentRun.ErrorMessage = ex.Message;
            agentRun.FinishedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(CancellationToken.None);

            _logger.LogError(ex,
                "[Agent] {AgentName} RunId={RunId} failed after {DurationMs}ms",
                agentName, context.RunId, stopwatch.ElapsedMilliseconds);

            return new AgentRunResult
            {
                Success = false,
                AgentName = agentName,
                ErrorMessage = ex.Message,
                Steps = steps,
                DurationMs = stopwatch.ElapsedMilliseconds,
            };
        }
    }

    /// <summary>
    /// P0：无工具 Agent，单次 LLM 调用。
    /// </summary>
    private async Task<AgentRunResult> RunSimpleAsync(
        AgentDefinition definition,
        string userInput,
        AgentRunContext context,
        List<AgentStep> steps,
        CancellationToken cancellationToken)
    {
        context.CurrentStep = 1;

        var output = await _llmClient.ChatAsync(definition.SystemPrompt, userInput, cancellationToken);

        steps.Add(new AgentStep
        {
            Index = 1,
            Type = AgentStepType.LlmResponse,
            Content = output,
        });

        return new AgentRunResult
        {
            Success = true,
            Output = output,
            AgentName = definition.Name,
            Steps = steps,
        };
    }

    /// <summary>
    /// P1 预留：带工具调用的多步推理循环。
    /// 当前阶段抛出 NotSupportedException，P1 实现时替换。
    /// </summary>
    private Task<AgentRunResult> RunWithToolsAsync(
        AgentDefinition definition,
        string userInput,
        AgentRunContext context,
        List<AgentStep> steps,
        CancellationToken cancellationToken)
    {
        // TODO P1: 实现 tool_call 循环
        // 1. 构建 messages[] + tools[]
        // 2. 调用 LLM (需要扩展 ILlmClient 支持 function calling)
        // 3. 如果返回 tool_calls → 执行工具 → 回填 tool role message → 重复
        // 4. 如果返回纯 content → 结束
        // 5. 超过 MaxSteps → 中止

        throw new NotSupportedException(
            $"Agent '{definition.Name}' declares {definition.ToolNames.Count} tools, " +
            "but tool-calling loop is not yet implemented (planned for P1). " +
            "Use ToolNames = [] for P0 agents.");
    }

    /// <summary>
    /// 检查 Agent 是否启用（Feature Flag）。
    /// 规则：Agents:Enabled（总开关）&& Agents:{agentName}:Enabled（单 Agent 开关，默认 true）
    /// </summary>
    private bool IsEnabled(string agentName)
    {
        var globalEnabled = _configuration.GetValue("Agents:Enabled", true);
        if (!globalEnabled) return false;

        var agentEnabled = _configuration.GetValue($"Agents:{agentName}:Enabled", true);
        return agentEnabled;
    }

    private static string? Truncate(string? text, int maxLength)
        => text is null || text.Length <= maxLength ? text : text[..maxLength];
}
