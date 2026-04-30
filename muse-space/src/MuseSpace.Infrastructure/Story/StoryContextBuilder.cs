using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Memory;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Abstractions.Story;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Story;

/// <summary>
/// Builds StoryContext by aggregating data from multiple repositories.
/// Budget（C-2/C-3）：
/// - 最近章节摘要：取最近 3 章。
/// - 角色卡：最多 4 张。
/// - 世界观规则：按 Priority DESC 取最多 8 条。
/// - 原著片段：top-K 召回后按相似度阈值过滤，每段截断到 <see cref="NovelSnippetMaxChars"/>，
///   全部片段总长度受 <see cref="NovelContextTotalCharBudget"/> 控制，避免 Prompt 膨胀。
/// 优先级：WorldRule > CharacterCards > NovelSnippets > RecentChapterSummaries。
/// 由 Prompt 模板保证：硬约束（角色 / 规则）在前，原著片段作为软约束在后。
/// </summary>
public sealed class StoryContextBuilder : IStoryContextBuilder
{
    /// <summary>原著检索召回 top-K。</summary>
    private const int NovelTopK = 5;

    /// <summary>低于该相似度的原著片段视为弱相关，直接丢弃。</summary>
    private const double NovelSimilarityThreshold = 0.3;

    /// <summary>单个原著片段最大字符数（超出则尾部截断 + 省略号）。</summary>
    private const int NovelSnippetMaxChars = 800;

    /// <summary>所有原著片段累计字符数预算，超出后停止追加。</summary>
    private const int NovelContextTotalCharBudget = 3000;

    private readonly IStoryProjectRepository _projectRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly IWorldRuleRepository _worldRuleRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly IStyleProfileRepository _styleProfileRepo;
    private readonly INovelMemorySearchService _novelMemorySearchService;
    private readonly ILogger<StoryContextBuilder> _logger;

    public StoryContextBuilder(
        IStoryProjectRepository projectRepo,
        ICharacterRepository characterRepo,
        IWorldRuleRepository worldRuleRepo,
        IChapterRepository chapterRepo,
        IStyleProfileRepository styleProfileRepo,
        INovelMemorySearchService novelMemorySearchService,
        ILogger<StoryContextBuilder> logger)
    {
        _projectRepo = projectRepo;
        _characterRepo = characterRepo;
        _worldRuleRepo = worldRuleRepo;
        _chapterRepo = chapterRepo;
        _styleProfileRepo = styleProfileRepo;
        _novelMemorySearchService = novelMemorySearchService;
        _logger = logger;
    }

    public async Task<StoryContext> BuildAsync(StoryContextRequest request, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepo.GetByIdAsync(request.StoryProjectId, cancellationToken);

        var chapters = await _chapterRepo.GetByProjectAsync(request.StoryProjectId, cancellationToken);
        var recentSummaries = chapters
            .Where(c => !string.IsNullOrWhiteSpace(c.Summary))
            .OrderByDescending(c => c.Number)
            .Take(3)
            .Select(c => $"第 {c.Number} 章《{c.Title ?? "无标题"}》…{c.Summary}")
            .ToList();

        var characters = await _characterRepo.GetByProjectAsync(request.StoryProjectId, cancellationToken);
        var characterPool = request.InvolvedCharacterIds?.Count > 0
            ? characters.Where(c => request.InvolvedCharacterIds.Contains(c.Id)).ToList()
            : characters;
        var characterCards = characterPool
            .Take(4)
            .Select(FormatCharacterCard)
            .ToList();

        var rules = await _worldRuleRepo.GetByProjectAsync(request.StoryProjectId, cancellationToken);
        var worldRules = rules
            .OrderByDescending(r => r.Priority)
            .Take(8)
            .Select(r => $"[{r.Category ?? "通用"}]{(r.IsHardConstraint ? "【强制】" : "")} {r.Title}：{r.Description}")
            .ToList();

        var styleProfile = await _styleProfileRepo.GetByProjectAsync(request.StoryProjectId, cancellationToken);
        var novelSnippets = await GetNovelContextSnippetsAsync(request, cancellationToken);

        return new StoryContext
        {
            ProjectSummary = project is not null
                ? $"《{project.Name}》{(project.Genre is not null ? $"【{project.Genre}】" : "")}：{project.Description}"
                : null,
            RecentChapterSummaries = recentSummaries,
            InvolvedCharacterCards = characterCards,
            WorldRules = worldRules,
            StyleRequirement = FormatStyleRequirement(styleProfile),
            SceneGoal = request.SceneGoal,
            Conflict = request.Conflict,
            EmotionCurve = request.EmotionCurve,
            NovelContextSnippets = novelSnippets
        };
    }

    private async Task<List<string>> GetNovelContextSnippetsAsync(
        StoryContextRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SceneGoal))
        {
            _logger.LogDebug(
                "[StoryContext] novel snippets skipped, SceneGoal empty (project={ProjectId})",
                request.StoryProjectId);
            return [];
        }

        try
        {
            var results = await _novelMemorySearchService.SearchAsync(
                request.StoryProjectId, request.SceneGoal, topK: NovelTopK, ct: cancellationToken);

            var aboveThreshold = results.Where(r => r.Similarity > NovelSimilarityThreshold).ToList();

            // 按预算追加：超出 NovelContextTotalCharBudget 后停止
            var injected = new List<string>(aboveThreshold.Count);
            var totalChars = 0;
            foreach (var r in aboveThreshold)
            {
                var snippet = TruncateWithEllipsis(r.Content, NovelSnippetMaxChars);
                if (totalChars + snippet.Length > NovelContextTotalCharBudget) break;
                injected.Add(snippet);
                totalChars += snippet.Length;
            }

            _logger.LogInformation(
                "[StoryContext] novel snippets project={ProjectId} retrieved={Retrieved} aboveThreshold={Above} injected={Injected} chars={Chars}/{Budget} topK={TopK} threshold={Threshold}",
                request.StoryProjectId, results.Count, aboveThreshold.Count,
                injected.Count, totalChars, NovelContextTotalCharBudget,
                NovelTopK, NovelSimilarityThreshold);

            return injected;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[StoryContext] novel snippet retrieval failed, falling back to empty (project={ProjectId})",
                request.StoryProjectId);
            return [];
        }
    }

    private static string TruncateWithEllipsis(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars) return text;
        return text[..maxChars] + "…";
    }

    private static string FormatCharacterCard(Character c)
    {
        var parts = new List<string> { $"【{c.Name}】" };
        if (c.Age.HasValue) parts.Add($"{c.Age}岁");
        if (!string.IsNullOrWhiteSpace(c.Role)) parts.Add(c.Role);
        if (!string.IsNullOrWhiteSpace(c.PersonalitySummary)) parts.Add($"性格：{c.PersonalitySummary}");
        if (!string.IsNullOrWhiteSpace(c.Motivation)) parts.Add($"动机：{c.Motivation}");
        if (!string.IsNullOrWhiteSpace(c.SpeakingStyle)) parts.Add($"说话方式：{c.SpeakingStyle}");
        if (!string.IsNullOrWhiteSpace(c.CurrentState)) parts.Add($"当前状态：{c.CurrentState}");
        if (!string.IsNullOrWhiteSpace(c.ForbiddenBehaviors)) parts.Add($"禁止行为：{c.ForbiddenBehaviors}");
        return string.Join("；", parts);
    }

    private static string? FormatStyleRequirement(StyleProfile? profile)
    {
        if (profile is null) return null;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(profile.Tone)) parts.Add($"语调：{profile.Tone}");
        if (!string.IsNullOrWhiteSpace(profile.SentenceLengthPreference)) parts.Add($"句式：{profile.SentenceLengthPreference}");
        if (!string.IsNullOrWhiteSpace(profile.DialogueRatio)) parts.Add($"对话比例：{profile.DialogueRatio}");
        if (!string.IsNullOrWhiteSpace(profile.DescriptionDensity)) parts.Add($"描写密度：{profile.DescriptionDensity}");
        if (!string.IsNullOrWhiteSpace(profile.ForbiddenExpressions)) parts.Add($"禁用表达：{profile.ForbiddenExpressions}");
        return parts.Count > 0 ? string.Join("；", parts) : null;
    }
}