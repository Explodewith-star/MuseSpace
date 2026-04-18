using MuseSpace.Application.Abstractions.Logging;
using MuseSpace.Application.Abstractions.Skills;
using MuseSpace.Contracts.Draft;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Services.Drafting;

/// <summary>
/// 场景草稿生成的应用服务，是该业务流程的唯一入口。
///
/// 调用链：
///   GenerateSceneDraftAppService
///     → ISkillOrchestrator（按 TaskType 路由）
///       → SceneDraftSkill
///           → IStoryContextBuilder  （组装背景、角色、规则等上下文）
///           → IPromptTemplateProvider（从文件系统加载 .md 模板）
///           → IPromptTemplateRenderer（渲染 {{变量}} 占位符）
///           → ILlmClient            （调用语言模型）
///     → IGenerationLogService（写入本地日志文件）
/// </summary>
public sealed class GenerateSceneDraftAppService
{
    private readonly ISkillOrchestrator _orchestrator;
    private readonly IGenerationLogService _logService;

    public GenerateSceneDraftAppService(
        ISkillOrchestrator orchestrator,
        IGenerationLogService logService)
    {
        _orchestrator = orchestrator;
        _logService = logService;
    }

    public async Task<GenerateSceneDraftResponse> ExecuteAsync(
        GenerateSceneDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N")[..12];

        var skillRequest = new SkillRequest
        {
            TaskType = "scene-draft",
            StoryProjectId = request.StoryProjectId,
            Parameters = new Dictionary<string, string>
            {
                ["SceneGoal"] = request.SceneGoal,
                ["Conflict"] = request.Conflict ?? string.Empty,
                ["EmotionCurve"] = request.EmotionCurve ?? string.Empty
            }
        };

        var result = await _orchestrator.ExecuteAsync(skillRequest, cancellationToken);

        var logRecord = new GenerationRecord
        {
            RequestId = requestId,
            StoryProjectId = request.StoryProjectId,
            TaskType = "scene-draft",
            SkillName = result.SkillName,
            PromptVersion = result.PromptVersion,
            ModelName = "gpt-oss",
            DurationMs = result.DurationMs,
            Success = result.Success,
            ErrorMessage = result.ErrorMessage,
            InputPreview = Truncate(request.SceneGoal, 200),
            OutputPreview = Truncate(result.Output, 500)
        };

        await _logService.LogAsync(logRecord, cancellationToken);

        return new GenerateSceneDraftResponse
        {
            RequestId = requestId,
            GeneratedText = result.Output,
            SkillName = result.SkillName,
            PromptVersion = result.PromptVersion,
            DurationMs = result.DurationMs
        };
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
