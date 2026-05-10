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
                ChapterId = TryParseGuid(request.Parameters.GetValueOrDefault("ChapterId")),
                OutlineId = TryParseGuid(request.Parameters.GetValueOrDefault("OutlineId")),
                InvolvedCharacterIds = ParseGuidList(request.Parameters.GetValueOrDefault("InvolvedCharacterIds")),
                SceneGoal = request.Parameters.GetValueOrDefault("SceneGoal", string.Empty),
                Conflict = request.Parameters.GetValueOrDefault("Conflict"),
                EmotionCurve = request.Parameters.GetValueOrDefault("EmotionCurve"),
                IncludeNovelContext = bool.TryParse(
                    request.Parameters.GetValueOrDefault("IncludeNovelContext"), out var includeNovelContext)
                    && includeNovelContext,
                // Module E
                GenerationMode = Enum.TryParse<Domain.Enums.GenerationMode>(
                    request.Parameters.GetValueOrDefault("GenerationMode"), out var gm)
                    ? gm : Domain.Enums.GenerationMode.Original,
                SourceNovelId = TryParseGuid(request.Parameters.GetValueOrDefault("SourceNovelId")),
                ContinuationStartChapterNumber = int.TryParse(
                    request.Parameters.GetValueOrDefault("ContinuationStartChapterNumber"), out var csn) ? csn : null,
                OriginalRangeStart = int.TryParse(
                    request.Parameters.GetValueOrDefault("OriginalRangeStart"), out var ors) ? ors : null,
                OriginalRangeEnd = int.TryParse(
                    request.Parameters.GetValueOrDefault("OriginalRangeEnd"), out var ore) ? ore : null,
                BranchTopic = request.Parameters.GetValueOrDefault("BranchTopic"),
                DivergencePolicy = Enum.TryParse<Domain.Enums.DivergencePolicy>(
                    request.Parameters.GetValueOrDefault("DivergencePolicy"), out var dp)
                    ? dp : Domain.Enums.DivergencePolicy.SoftCanon,
            };
            var storyContext = await _contextBuilder.BuildAsync(contextRequest, cancellationToken);

            // 2. Load prompt template
            var template = await _promptProvider.GetTemplateAsync("drafting", "scene-v1", cancellationToken);

            // 3. Build variables from context
            var variables = new Dictionary<string, string>
            {
                ["project_summary"] = storyContext.ProjectSummary ?? string.Empty,
                ["outline_summary"] = storyContext.OutlineSummary ?? string.Empty,
                ["recent_summaries"] = string.Join("\n", storyContext.RecentChapterSummaries),
                ["character_cards"] = string.Join("\n", storyContext.InvolvedCharacterCards),
                ["world_rules"] = string.Join("\n", storyContext.WorldRules),
                ["style_requirement"] = storyContext.StyleRequirement ?? string.Empty,
                ["chapter_plan_contract"] = request.Parameters.GetValueOrDefault("ChapterPlanContract", string.Empty),
                ["scene_goal"] = storyContext.SceneGoal,
                ["conflict"] = storyContext.Conflict ?? string.Empty,
                ["emotion_curve"] = storyContext.EmotionCurve ?? string.Empty,
                ["novel_context"] = storyContext.NovelContextSnippets.Count > 0
                    ? string.Join("\n\n---\n\n", storyContext.NovelContextSnippets)
                    : string.Empty,
                ["reference_text"] = request.Parameters.GetValueOrDefault("ReferenceText", string.Empty),
                ["reference_focus"] = BuildReferenceFocusText(
                    request.Parameters.GetValueOrDefault("ReferenceFocus", string.Empty)),
                ["reference_strength"] = BuildReferenceStrengthText(
                    request.Parameters.GetValueOrDefault("ReferenceStrength", string.Empty)),
                ["timeline_events"] = storyContext.RecentEvents.Count > 0
                    ? string.Join("\n", storyContext.RecentEvents)
                    : string.Empty,
                ["character_states"] = storyContext.CharacterStateFacts.Count > 0
                    ? string.Join("\n", storyContext.CharacterStateFacts)
                    : string.Empty,
                ["immutable_facts"] = storyContext.ImmutableFacts.Count > 0
                    ? string.Join("\n", storyContext.ImmutableFacts)
                    : string.Empty,
                // Module E variables
                ["generation_mode_header"] = storyContext.GenerationModeHeader ?? string.Empty,
                ["novel_ending"] = BuildNovelEndingSection(
                    storyContext.NovelEndingSummary, storyContext.NovelEndingSnippets),
                ["novel_character_end_states"] = storyContext.NovelCharacterEndStates.Count > 0
                    ? string.Join("\n", storyContext.NovelCharacterEndStates)
                    : string.Empty,
                ["novel_style_summary"] = storyContext.NovelStyleSummary ?? string.Empty,
                ["branch_context"] = BuildBranchContextSection(
                    request.Parameters.GetValueOrDefault("BranchTopic"),
                    storyContext.BranchContextSnippets),
                ["divergence_policy"] = storyContext.DivergencePolicyNote ?? string.Empty,
            };

            // 4. Render prompts
            var systemPrompt = _promptRenderer.RenderSystemPrompt(template, variables);
            var userPrompt = _promptRenderer.RenderUserPrompt(template, variables);

            // 5. Call LLM
            var llmResult = await _llmClient.ChatAsync(systemPrompt, userPrompt, cancellationToken);

            // 6. 宽松解析 JSON 输出：成功则提取 scene_text，失败则保留原始文本
            var parsedOutput = TryExtractSceneText(llmResult.Content);

            stopwatch.Stop();

            var renderedPrompt =
                "===== SYSTEM =====\n" + systemPrompt +
                "\n\n===== USER =====\n" + userPrompt;

            return new SkillResult
            {
                Success = true,
                Output = parsedOutput,
                SkillName = Name,
                PromptVersion = template.Version,
                DurationMs = stopwatch.ElapsedMilliseconds,
                InputTokens = llmResult.InputTokens,
                OutputTokens = llmResult.OutputTokens,
                RenderedPrompt = renderedPrompt,
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

    private static string BuildReferenceFocusText(string value) => value switch
    {
        "Emotion" => "主要参考情绪氛围，例如紧张、暧昧、压抑、悲伤或释然的推进方式。",
        "Dialogue" => "主要参考对话方式，例如句子长短、停顿、含蓄程度、攻防节奏和人物回应方式。",
        "NarrativeRhythm" => "主要参考叙事节奏，例如铺垫、转场、悬念释放和段落推进速度。",
        "StyleTexture" => "主要参考文风质感，例如描写密度、句式倾向、画面感和语言颗粒度。",
        "SceneStructure" => "主要参考场景结构，例如开场、推进、转折、收束的组织方式。",
        "InteractionTension" => "主要参考人物互动张力，例如关系拉扯、潜台词、试探与情绪变化。",
        _ => string.Empty
    };

    private static string BuildReferenceStrengthText(string value) => value switch
    {
        "Low" => "轻度参考：只吸收大方向，优先遵循本章大纲与项目事实。",
        "High" => "强参考：明显吸收所选参考维度，但仍不得复述、改写或照搬片段内容。",
        "Medium" => "中度参考：在所选维度上适当靠近参考片段，同时保持本项目原有设定和剧情。",
        _ => string.Empty
    };

    private static Guid? TryParseGuid(string? raw)
        => Guid.TryParse(raw, out var g) ? g : null;

    private static List<Guid>? ParseGuidList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var ids = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => Guid.TryParse(item, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        return ids.Count > 0 ? ids : null;
    }

    private static string BuildBranchContextSection(string? branchTopic, List<string> snippets)
    {
        if (string.IsNullOrWhiteSpace(branchTopic) && snippets.Count == 0)
            return string.Empty;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(branchTopic))
            parts.Add($"番外主题：{branchTopic}");
        if (snippets.Count > 0)
            parts.Add(string.Join("\n\n---\n\n", snippets));
        return string.Join("\n\n", parts);
    }

    private static string BuildNovelEndingSection(string? endingSummary, List<string> rawSnippets)
    {
        if (!string.IsNullOrWhiteSpace(endingSummary))
            return $"【大结局摘要】\n{endingSummary}" +
                (rawSnippets.Count > 0
                    ? $"\n\n【衔接锚点（紧邻原著末段）】\n{string.Join("\n\n---\n\n", rawSnippets)}"
                    : string.Empty);
        if (rawSnippets.Count > 0)
            return string.Join("\n\n---\n\n", rawSnippets);
        return string.Empty;
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
