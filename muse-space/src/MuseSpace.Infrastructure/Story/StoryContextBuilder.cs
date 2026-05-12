using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Memory;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Abstractions.Story;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

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
    private readonly IStoryOutlineRepository _outlineRepo;
    private readonly IStyleProfileRepository _styleProfileRepo;
    private readonly INovelMemorySearchService _novelMemorySearchService;
    private readonly IChapterEventRepository _eventRepo;
    private readonly ICanonFactRepository _factRepo;
    private readonly INovelChunkRepository _novelChunkRepo;
    private readonly INovelRepository _novelRepo;
    private readonly INovelCharacterSnapshotRepository _snapshotRepo;
    private readonly IPlotThreadRepository _plotThreadRepo;
    private readonly ILogger<StoryContextBuilder> _logger;

    public StoryContextBuilder(
        IStoryProjectRepository projectRepo,
        ICharacterRepository characterRepo,
        IWorldRuleRepository worldRuleRepo,
        IChapterRepository chapterRepo,
        IStoryOutlineRepository outlineRepo,
        IStyleProfileRepository styleProfileRepo,
        INovelMemorySearchService novelMemorySearchService,
        IChapterEventRepository eventRepo,
        ICanonFactRepository factRepo,
        INovelChunkRepository novelChunkRepo,
        INovelRepository novelRepo,
        INovelCharacterSnapshotRepository snapshotRepo,
        IPlotThreadRepository plotThreadRepo,
        ILogger<StoryContextBuilder> logger)
    {
        _projectRepo = projectRepo;
        _characterRepo = characterRepo;
        _worldRuleRepo = worldRuleRepo;
        _chapterRepo = chapterRepo;
        _outlineRepo = outlineRepo;
        _styleProfileRepo = styleProfileRepo;
        _novelMemorySearchService = novelMemorySearchService;
        _eventRepo = eventRepo;
        _factRepo = factRepo;
        _novelChunkRepo = novelChunkRepo;
        _novelRepo = novelRepo;
        _snapshotRepo = snapshotRepo;
        _plotThreadRepo = plotThreadRepo;
        _logger = logger;
    }

    public async Task<StoryContext> BuildAsync(StoryContextRequest request, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepo.GetByIdAsync(request.StoryProjectId, cancellationToken);

        var chapters = await _chapterRepo.GetByProjectAsync(request.StoryProjectId, cancellationToken);
        var outlines = await _outlineRepo.GetByProjectAsync(request.StoryProjectId, cancellationToken);
        var currentChapter = request.ChapterId.HasValue
            ? chapters.FirstOrDefault(c => c.Id == request.ChapterId.Value)
            : null;
        var currentOutlineId = request.OutlineId
            ?? currentChapter?.StoryOutlineId
            ?? outlines.FirstOrDefault(o => o.IsDefault)?.Id;
        var scopedChapters = currentOutlineId.HasValue
            ? chapters.Where(c => c.StoryOutlineId == currentOutlineId.Value).ToList()
            : chapters;
        var currentOutline = currentOutlineId.HasValue
            ? outlines.FirstOrDefault(o => o.Id == currentOutlineId.Value)
            : null;

        // 若知道当前章节，只取编号严格小于当前章的章节作为"已发生"上下文，
        // 避免把后续章节的摘要/事件错误地注入 Prompt 导致内容错位。
        var currentChapterNumber = currentChapter?.Number;
        var priorChapters = currentChapterNumber.HasValue
            ? scopedChapters.Where(c => c.Number < currentChapterNumber.Value).ToList()
            : scopedChapters;

        var recentSummaries = priorChapters
            .Where(c => !string.IsNullOrWhiteSpace(c.Summary))
            .OrderByDescending(c => c.Number)
            .Take(3)
            .Select(c => $"第 {c.Number} 章《{c.Title ?? "无标题"}》…{c.Summary}")
            .ToList();

        var characters = await _characterRepo.GetByProjectAsync(request.StoryProjectId, cancellationToken);

        // 原创模式下，排除从原著提取的角色（SourceNovelId != null），只使用用户原创角色
        var filteredCharacters = request.GenerationMode == GenerationMode.Original
            ? characters.Where(c => c.SourceNovelId == null).ToList()
            : characters;

        var characterPool = request.InvolvedCharacterIds?.Count > 0
            ? filteredCharacters.Where(c => request.InvolvedCharacterIds.Contains(c.Id)).ToList()
            : filteredCharacters;
        var characterCards = characterPool
            .Take(4)
            .Select(FormatCharacterCard)
            .ToList();

        var rules = await _worldRuleRepo.GetByProjectAsync(request.StoryProjectId, cancellationToken);

        // 原创模式下，排除从原著提取的世界观规则
        var filteredRules = request.GenerationMode == GenerationMode.Original
            ? rules.Where(r => r.SourceNovelId == null).ToList()
            : rules;

        var worldRules = filteredRules
            .OrderByDescending(r => r.Priority)
            .Take(8)
            .Select(r => $"[{r.Category ?? "通用"}]{(r.IsHardConstraint ? "【强制】" : "")} {r.Title}：{r.Description}")
            .ToList();

        var styleProfile = await _styleProfileRepo.GetByProjectAsync(request.StoryProjectId, cancellationToken);
        var novelSnippets = await GetNovelContextSnippetsAsync(request, cancellationToken);

        // Module D 正典事实层注入。
        // 1. 最近事件：取最近 3 个有事件的章节，依照 “第N章 - 事件” 拼接。
        // 2. 人物状态快照：Active（未被废弃）且已锁定的 Relationship / Identity / LifeStatus。
        // 3. 不可重复事实：已锁定的 UniqueEvent 事实 + 所有历史不可逆事件。
        var priorChapterIds = priorChapters.Select(c => c.Id).ToHashSet();
        var recentEventChapterIds = priorChapters
            .OrderByDescending(c => c.Number)
            .Take(3)
            .Select(c => c.Id)
            .ToHashSet();
        var chapterById = scopedChapters.ToDictionary(c => c.Id, c => c);
        var projectEvents = await _eventRepo.GetByProjectAsync(request.StoryProjectId, cancellationToken);
        var recentEventLines = projectEvents
            .Where(e => recentEventChapterIds.Contains(e.ChapterId))
            .OrderBy(e => chapterById.TryGetValue(e.ChapterId, out var ch) ? ch.Number : int.MaxValue)
            .ThenBy(e => e.Order)
            .Select(e =>
            {
                var num = chapterById.TryGetValue(e.ChapterId, out var ch) ? $"第 {ch.Number} 章" : "未知章";
                var marker = e.IsIrreversible ? "★" : "·";
                return $"{num} {marker} [{e.EventType}] {e.EventText}";
            })
            .ToList();

        var scopedActiveFacts = await GetFactsVisibleToChapterAsync(
            request, chapters, cancellationToken);
        var stateFacts = scopedActiveFacts
            .Where(f => f.IsLocked &&
                (f.FactType == "Relationship" || f.FactType == "Identity" || f.FactType == "LifeStatus"))
            .Select(f => $"[{f.FactType}] {f.FactKey} = {f.FactValue}")
            .ToList();

        var immutableFromFacts = scopedActiveFacts
            .Where(f => f.IsLocked && f.FactType == "UniqueEvent")
            .Select(f => $"★ {f.FactKey} 已发生（{f.FactValue}）")
            .ToList();
        // 则外带上过往不可逆事件（可能尚未被 Canon 抽取为 UniqueEvent）
        var immutableFromEvents = projectEvents
            .Where(e => !currentChapterNumber.HasValue || priorChapterIds.Contains(e.ChapterId))
            .Where(e => e.IsIrreversible)
            .OrderBy(e => chapterById.TryGetValue(e.ChapterId, out var ch) ? ch.Number : int.MaxValue)
            .ThenBy(e => e.Order)
            .Select(e =>
            {
                var num = chapterById.TryGetValue(e.ChapterId, out var ch) ? $"第 {ch.Number} 章" : "未知章";
                return $"★ {num} [{e.EventType}] {e.EventText}";
            })
            .ToList();
        var immutableFacts = immutableFromFacts.Concat(immutableFromEvents).Distinct().ToList();

        // Module E：续写/外传模式上下文
        var modeResult = await GetModeContextAsync(request, cancellationToken);

        // P2-⑨：按作用域过滤活跃伏笔，注入 Prompt
        List<string> activePlotThreadLines = [];
        if (currentOutlineId.HasValue)
        {
            var visibleThreads = await _plotThreadRepo.GetVisibleToOutlineAsync(
                request.StoryProjectId,
                currentOutlineId.Value,
                currentOutline?.ChainId,
                cancellationToken);
            activePlotThreadLines = visibleThreads
                .Take(10)
                .Select(t => $"[{t.Importance ?? "Medium"}] {t.Title}：{t.Description ?? ""}")
                .ToList();
        }

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
            NovelContextSnippets = novelSnippets,
            RecentEvents = recentEventLines,
            CharacterStateFacts = stateFacts,
            ImmutableFacts = immutableFacts,
            GenerationModeHeader = modeResult.Header,
            NovelEndingSnippets = modeResult.EndingSnippets,
            BranchContextSnippets = modeResult.BranchSnippets,
            DivergencePolicyNote = modeResult.PolicyNote,
            NovelEndingSummary = modeResult.EndingSummary,
            NovelStyleSummary = modeResult.StyleSummary,
            NovelCharacterEndStates = modeResult.CharacterEndStates,
            OutlineSummary = currentOutline is not null ? BuildOutlineSummary(currentOutline) : null,
            ActivePlotThreads = activePlotThreadLines,
        };
    }

    // ── Module E：获取续写/支线上下文 ──────────────────────────────────

    private sealed record ModeContext(
        string? Header,
        List<string> EndingSnippets,
        List<string> BranchSnippets,
        string? PolicyNote,
        string? EndingSummary,
        string? StyleSummary,
        List<string> CharacterEndStates);

    private async Task<ModeContext> GetModeContextAsync(
        StoryContextRequest request, CancellationToken ct)
    {
        if (request.GenerationMode == GenerationMode.Original)
            return new ModeContext(null, [], [], null, null, null, []);

        string header = request.GenerationMode switch
        {
            GenerationMode.ContinueFromOriginal => "【创作模式：原著续写】本章为导入原著的后续延伸，请在原著结局基础上自然衔接。",
            GenerationMode.SideStoryFromOriginal => "【创作模式：支线番外】本章为原著衍生支线/番外，角色与世界观遵循原著设定。",
            GenerationMode.ExpandOrRewrite => "【创作模式：扩写/改写】本章对既有内容进行扩展或再创作，保持叙事连贯性。",
            _ => string.Empty,
        };

        string? policyNote = request.DivergencePolicy switch
        {
            DivergencePolicy.StrictCanon => "⚠️ 严格正典约束：禁止改写原著已确定的事实、结局与人物关系，所有新增情节须在原著框架内。",
            DivergencePolicy.SoftCanon => "提示（软正典）：允许局部补写与细节扩展，但不得推翻原著关键设定与人物命运。",
            DivergencePolicy.AlternateTimeline => "声明（平行线）：本章为平行宇宙架空情节，可在合理范围内偏离原著走向。",
            _ => null,
        };

        var endingSnippets = new List<string>();
        var branchSnippets = new List<string>();
        string? endingSummary = null;
        string? styleSummary = null;
        var characterEndStates = new List<string>();

        if (request.SourceNovelId.HasValue)
        {
            try
            {
                // 优先读取预生成的摘要和角色末态
                var novel = await _novelRepo.GetByIdAsync(request.SourceNovelId.Value, ct);
                if (novel is not null)
                {
                    endingSummary = novel.EndingSummary;
                    styleSummary = novel.StyleSummary;
                }

                var snapshots = await _snapshotRepo.GetByNovelAsync(request.SourceNovelId.Value, ct);
                characterEndStates = snapshots
                    .Select(s => $"【{s.CharacterName}】{(s.IsIrreversible ? "★ " : "")}{s.EndingState}")
                    .ToList();

                // ── 续写模式：语义检索 + tail 锚点 ────────────────────────────
                if (request.GenerationMode == GenerationMode.ContinueFromOriginal)
                {
                    // 若已有结局摘要，raw chunk 只保留最后 1 个作为衔接锚点，减少 Token
                    if (!string.IsNullOrWhiteSpace(endingSummary))
                    {
                        var allChunks = (await _novelChunkRepo.GetByNovelAsync(request.SourceNovelId.Value, ct))
                            .OrderBy(c => c.ChunkIndex).ToList();
                        var anchorChunks = allChunks.TakeLast(1).ToList();
                        endingSnippets = anchorChunks
                            .Select(c => TruncateWithEllipsis(c.Content, NovelSnippetMaxChars))
                            .ToList();
                    }
                    else
                    {
                        // 无摘要降级：语义检索（top-4）+ tail（最后 2 chunk）混合
                        var semanticResults = await _novelMemorySearchService.SearchByNovelAsync(
                            request.SourceNovelId.Value, request.SceneGoal, topK: 4, ct: ct);
                        var semanticSnippets = semanticResults
                            .Where(r => r.Similarity > NovelSimilarityThreshold)
                            .Select(r => TruncateWithEllipsis(r.Content, NovelSnippetMaxChars))
                            .ToList();

                        var allChunks = (await _novelChunkRepo.GetByNovelAsync(request.SourceNovelId.Value, ct))
                            .OrderBy(c => c.ChunkIndex).ToList();
                        var tailSnippets = allChunks.TakeLast(2)
                            .Select(c => TruncateWithEllipsis(c.Content, NovelSnippetMaxChars))
                            .ToList();

                        // 合并去重（tail 在前保证时序锚点）
                        endingSnippets = tailSnippets
                            .Concat(semanticSnippets)
                            .Distinct()
                            .Take(5)
                            .ToList();
                    }
                }
                // ── 支线番外模式：语义检索（按指定范围或全文）────────────────
                else if (request.GenerationMode == GenerationMode.SideStoryFromOriginal)
                {
                    if (request.OriginalRangeStart.HasValue || request.OriginalRangeEnd.HasValue)
                    {
                        // 指定了范围：直接切片
                        var allChunks = (await _novelChunkRepo.GetByNovelAsync(request.SourceNovelId.Value, ct))
                            .OrderBy(c => c.ChunkIndex).ToList();
                        var start = request.OriginalRangeStart ?? 0;
                        var end = request.OriginalRangeEnd ?? Math.Min(start + 5, allChunks.Count);
                        branchSnippets = allChunks
                            .Skip(start).Take(Math.Max(0, end - start)).Take(5)
                            .Select(c => TruncateWithEllipsis(c.Content, NovelSnippetMaxChars))
                            .ToList();
                    }
                    else
                    {
                        // 未指定范围：用番外主题或 SceneGoal 语义检索最相关 5 个 chunk
                        var query = !string.IsNullOrWhiteSpace(request.BranchTopic)
                            ? request.BranchTopic
                            : request.SceneGoal;
                        var semanticResults = await _novelMemorySearchService.SearchByNovelAsync(
                            request.SourceNovelId.Value, query, topK: 5, ct: ct);
                        branchSnippets = semanticResults
                            .Where(r => r.Similarity > NovelSimilarityThreshold)
                            .Select(r => TruncateWithEllipsis(r.Content, NovelSnippetMaxChars))
                            .ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[StoryContext] Mode context retrieval failed (novel={NovelId})",
                    request.SourceNovelId);
            }
        }

        return new ModeContext(header, endingSnippets, branchSnippets, policyNote,
            endingSummary, styleSummary, characterEndStates);
    }

    private async Task<List<string>> GetNovelContextSnippetsAsync(
        StoryContextRequest request, CancellationToken cancellationToken)
    {
        if (!request.IncludeNovelContext)
        {
            _logger.LogDebug(
                "[StoryContext] novel snippets skipped, IncludeNovelContext=false (project={ProjectId}, chapter={ChapterId})",
                request.StoryProjectId, request.ChapterId);
            return [];
        }

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

    private async Task<List<CanonFact>> GetFactsVisibleToChapterAsync(
        StoryContextRequest request,
        List<Chapter> chapters,
        CancellationToken cancellationToken)
    {
        var currentOutlineId = request.OutlineId;
        if (!currentOutlineId.HasValue && request.ChapterId.HasValue)
            currentOutlineId = chapters.FirstOrDefault(c => c.Id == request.ChapterId.Value)?.StoryOutlineId;
        var activeFacts = currentOutlineId.HasValue
            ? await _factRepo.GetActiveByOutlineAsync(request.StoryProjectId, currentOutlineId.Value, cancellationToken)
            : await _factRepo.GetActiveAsync(request.StoryProjectId, cancellationToken);
        if (!request.ChapterId.HasValue) return activeFacts;

        if (currentOutlineId.HasValue)
        {
            chapters = chapters.Where(c => c.StoryOutlineId == currentOutlineId.Value).ToList();
        }

        var currentChapterNumber = chapters.FirstOrDefault(c => c.Id == request.ChapterId.Value)?.Number;
        if (!currentChapterNumber.HasValue) return activeFacts;

        var chapterNumberById = chapters.ToDictionary(c => c.Id, c => c.Number);
        return activeFacts
            .Where(f => f.SourceChapterId is null
                || (chapterNumberById.TryGetValue(f.SourceChapterId.Value, out var sourceNumber)
                    && sourceNumber < currentChapterNumber.Value))
            .ToList();
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
        if (!string.IsNullOrWhiteSpace(c.SpeakingStyle)) parts.Add($"说话方式：{c.SpeakingStyle}");
        if (!string.IsNullOrWhiteSpace(c.ForbiddenBehaviors)) parts.Add($"禁止行为：{c.ForbiddenBehaviors}");
        // 注意：Motivation / CurrentState 故意不注入。
        // 这两个字段从原著抽取时通常代表「结局态/最终动机」（如 "已被鬼附身"、"为某人复仇"），
        // 注入后会让 LLM 写早期章节时直接演绎结局剧情，造成驴头不对马嘴。
        // 当前章节应处于的状态请通过章节计划（Conflict / MustIncludePoints / Goal）引导。
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

    private static string BuildOutlineSummary(StoryOutline outline)
    {
        var parts = new List<string> { $"【大纲】{outline.Name}" };
        parts.Add($"模式：{outline.Mode}");
        if (outline.IsDefault) parts.Add("默认主线");
        if (!string.IsNullOrWhiteSpace(outline.OutlineSummary)) parts.Add($"摘要：{outline.OutlineSummary}");
        if (!string.IsNullOrWhiteSpace(outline.BranchTopic)) parts.Add($"主题：{outline.BranchTopic}");
        if (!string.IsNullOrWhiteSpace(outline.ContinuationAnchor)) parts.Add($"锚点：{outline.ContinuationAnchor}");
        return string.Join("；", parts);
    }
}
