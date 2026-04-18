using System.Diagnostics;
using System.Text.Json;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Prompt;
using MuseSpace.Application.Abstractions.Skills;
using MuseSpace.Application.Abstractions.Story;

namespace MuseSpace.Application.Services.Drafting;

/// <summary>
/// 场景草稿 Skill，TaskType = "scene-draft"。
/// 执行流程：组装上下文 → 加载 Prompt 模板 → 渲染变量 → 调用 LLM → 返回结果。
/// </summary>
public sealed class SceneDraftSkill : ISkill
{
    private readonly ILlmClient _llmClient;
    private readonly IPromptTemplateProvider _promptProvider;
    private readonly IPromptTemplateRenderer _promptRenderer;
    private readonly IStoryContextBuilder _contextBuilder;

    public string Name => "SceneDraft";
    public string TaskType => "scene-draft";

    public SceneDraftSkill(
        ILlmClient llmClient,
        IPromptTemplateProvider promptProvider,
        IPromptTemplateRenderer promptRenderer,
        IStoryContextBuilder contextBuilder)
    {
        _llmClient = llmClient;
        _promptProvider = promptProvider;
        _promptRenderer = promptRenderer;
        _contextBuilder = contextBuilder;
    }

    public async Task<SkillResult> ExecuteAsync(SkillRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. Build story context
            var contextRequest = new StoryContextRequest
            {
                StoryProjectId = request.StoryProjectId,
                SceneGoal = request.Parameters.GetValueOrDefault("SceneGoal", string.Empty),
                Conflict = request.Parameters.GetValueOrDefault("Conflict"),
                EmotionCurve = request.Parameters.GetValueOrDefault("EmotionCurve")
            };
            var storyContext = await _contextBuilder.BuildAsync(contextRequest, cancellationToken);

            // 2. Load prompt template
            var template = await _promptProvider.GetTemplateAsync("drafting", "scene-v1", cancellationToken);

            // 3. Build variables from context
            var variables = new Dictionary<string, string>
            {
                ["project_summary"] = storyContext.ProjectSummary ?? string.Empty,
                ["recent_summaries"] = string.Join("\n", storyContext.RecentChapterSummaries),
                ["character_cards"] = string.Join("\n", storyContext.InvolvedCharacterCards),
                ["world_rules"] = string.Join("\n", storyContext.WorldRules),
                ["style_requirement"] = storyContext.StyleRequirement ?? string.Empty,
                ["scene_goal"] = storyContext.SceneGoal,
                ["conflict"] = storyContext.Conflict ?? string.Empty,
                ["emotion_curve"] = storyContext.EmotionCurve ?? string.Empty
            };

            // 4. Render prompts
            var systemPrompt = _promptRenderer.RenderSystemPrompt(template, variables);
            var userPrompt = _promptRenderer.RenderUserPrompt(template, variables);

            // 5. Call LLM
            var output = await _llmClient.ChatAsync(systemPrompt, userPrompt, cancellationToken);

            // 6. 宽松解析 JSON 输出：成功则提取 scene_text，失败则保留原始文本
            var parsedOutput = TryExtractSceneText(output);

            stopwatch.Stop();

            return new SkillResult
            {
                Success = true,
                Output = parsedOutput,
                SkillName = Name,
                PromptVersion = template.Version,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new SkillResult
            {
                Success = false,
                Output = string.Empty,
                ErrorMessage = ex.Message,
                SkillName = Name,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// 宽松 JSON 解析：尝试从模型输出中提取 scene_text 字段。
    /// 自动处理模型常见的 markdown 代码块包裹（```json ... ```）。
    /// 解析失败时不中断，直接返回原始文本。
    /// </summary>
    private static string TryExtractSceneText(string rawOutput)
    {
        try
        {
            var json = StripMarkdownCodeFence(rawOutput);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("scene_text", out var sceneText))
            {
                var text = sceneText.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }
        catch (JsonException)
        {
            // 模型未返回有效 JSON，降级使用原始文本
        }

        return rawOutput;
    }

    /// <summary>
    /// 去除模型输出中的 markdown 代码块包裹，例如：
    ///   ```json\n{...}\n```  →  {...}
    /// </summary>
    private static string StripMarkdownCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;

        var firstNewLine = trimmed.IndexOf('\n');
        if (firstNewLine < 0) return trimmed;

        var withoutOpenFence = trimmed[(firstNewLine + 1)..];

        var closingFence = withoutOpenFence.LastIndexOf("```");
        if (closingFence < 0) return withoutOpenFence.Trim();

        return withoutOpenFence[..closingFence].Trim();
    }
}
