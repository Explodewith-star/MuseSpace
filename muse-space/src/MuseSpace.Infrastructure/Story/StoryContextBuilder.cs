using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Abstractions.Story;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Story;

/// <summary>
/// 从 JSON 文件仓储中读取故事资料，按上下文预算拼装 StoryContext。
/// 预算：最近 3 章摘要、最多 4 个角色、最多 8 条世界规则（按 Priority 降序）。
/// </summary>
public sealed class StoryContextBuilder : IStoryContextBuilder
{
    private readonly IStoryProjectRepository _projectRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly IWorldRuleRepository _worldRuleRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly IStyleProfileRepository _styleProfileRepo;

    public StoryContextBuilder(
        IStoryProjectRepository projectRepo,
        ICharacterRepository characterRepo,
        IWorldRuleRepository worldRuleRepo,
        IChapterRepository chapterRepo,
        IStyleProfileRepository styleProfileRepo)
    {
        _projectRepo = projectRepo;
        _characterRepo = characterRepo;
        _worldRuleRepo = worldRuleRepo;
        _chapterRepo = chapterRepo;
        _styleProfileRepo = styleProfileRepo;
    }

    public async Task<StoryContext> BuildAsync(StoryContextRequest request, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepo.GetByIdAsync(request.StoryProjectId, cancellationToken);

        var chapters = await _chapterRepo.GetByProjectAsync(request.StoryProjectId, cancellationToken);
        var recentSummaries = chapters
            .Where(c => !string.IsNullOrWhiteSpace(c.Summary))
            .OrderByDescending(c => c.Number)
            .Take(3)
            .Select(c => $"第 {c.Number} 章《{c.Title ?? "无题"}》：{c.Summary}")
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
            .Select(r => $"[{r.Category ?? "规则"}]{(r.IsHardConstraint ? "【强制】" : "")} {r.Title}：{r.Description}")
            .ToList();

        var styleProfile = await _styleProfileRepo.GetByProjectAsync(request.StoryProjectId, cancellationToken);

        return new StoryContext
        {
            ProjectSummary = project is not null
                ? $"《{project.Name}》{(project.Genre is not null ? $"（{project.Genre}）" : "")}：{project.Description}"
                : null,
            RecentChapterSummaries = recentSummaries,
            InvolvedCharacterCards = characterCards,
            WorldRules = worldRules,
            StyleRequirement = FormatStyleRequirement(styleProfile),
            SceneGoal = request.SceneGoal,
            Conflict = request.Conflict,
            EmotionCurve = request.EmotionCurve
        };
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
        if (!string.IsNullOrWhiteSpace(c.ForbiddenBehaviors)) parts.Add($"不会做：{c.ForbiddenBehaviors}");
        return string.Join("，", parts);
    }

    private static string? FormatStyleRequirement(StyleProfile? profile)
    {
        if (profile is null) return null;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(profile.Tone)) parts.Add($"基调：{profile.Tone}");
        if (!string.IsNullOrWhiteSpace(profile.SentenceLengthPreference)) parts.Add($"句式：{profile.SentenceLengthPreference}");
        if (!string.IsNullOrWhiteSpace(profile.DialogueRatio)) parts.Add($"对话比例：{profile.DialogueRatio}");
        if (!string.IsNullOrWhiteSpace(profile.DescriptionDensity)) parts.Add($"描写密度：{profile.DescriptionDensity}");
        if (!string.IsNullOrWhiteSpace(profile.ForbiddenExpressions)) parts.Add($"禁用表达：{profile.ForbiddenExpressions}");
        return parts.Count > 0 ? string.Join("；", parts) : null;
    }
}
