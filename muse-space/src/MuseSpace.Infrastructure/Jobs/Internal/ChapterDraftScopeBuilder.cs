using System.Text.RegularExpressions;
using MuseSpace.Contracts.Chapters;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

namespace MuseSpace.Infrastructure.Jobs.Internal;

public static partial class ChapterDraftScopeBuilder
{
    private static readonly string[] SignatureNoiseTerms =
    [
        "一个", "一种", "自己", "他们", "我们", "已经", "开始", "没有", "不是", "时候", "地方", "事情", "感觉",
        "然后", "继续", "最终", "逐渐", "需要", "必须", "应该", "可以", "以及", "同时", "其中"
    ];

    private static readonly string[] LocationMarkers =
    [
        "宿舍", "教室", "走廊", "操场", "校园", "学校", "办公室", "地下室", "地窖",
        "工厂", "实验室", "医院", "村", "城市", "门后", "鬼域", "异空间", "房间"
    ];

    public static ChapterDraftScope Build(
        Guid projectId,
        Chapter chapter,
        StoryOutline outline,
        IReadOnlyList<Chapter> outlineChapters,
        GenerateChapterDraftRequest? options)
    {
        var currentPlanText = BuildPlanText(chapter);
        var futureChapters = outlineChapters
            .Where(c => c.Number > chapter.Number)
            .OrderBy(c => c.Number)
            .Take(5)
            .ToList();
        var futureBeats = futureChapters
            .Select(FormatFutureChapter)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        var futureChapterSignatures = futureChapters
            .Select(future => BuildFutureChapterSignature(future, currentPlanText))
            .Where(signature => signature is not null)
            .Cast<FutureChapterSignature>()
            .ToList();
        var revealLevel = chapter.AllowedRevealLevel ?? InferRevealLevel(chapter);
        var mode = outline.Mode;
        var boundaryInstruction = BuildBoundaryInstruction(chapter, outline, revealLevel, futureBeats);

        return new ChapterDraftScope
        {
            ProjectId = projectId,
            ChapterId = chapter.Id,
            OutlineId = outline.Id,
            ChapterNumber = chapter.Number,
            CurrentPlanText = currentPlanText,
            RequiredBeats = chapter.MustIncludePoints?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? [],
            AllowedLocations = ExtractAllowedLocations(currentPlanText),
            AllowedRevealLevel = revealLevel,
            GenerationMode = mode,
            DivergencePolicy = outline.DivergencePolicy,
            SourceNovelId = outline.SourceNovelId ?? options?.SourceNovelId,
            SourceRangeStart = outline.SourceRangeStart ?? options?.OriginalRangeStart,
            SourceRangeEnd = outline.SourceRangeEnd ?? options?.OriginalRangeEnd,
            BranchTopic = !string.IsNullOrWhiteSpace(outline.BranchTopic)
                ? outline.BranchTopic
                : options?.BranchTopic,
            ContinuationAnchor = outline.ContinuationAnchor,
            ReservedFutureBeats = futureBeats,
            FutureChapters = futureChapters,
            FutureChapterSignatures = futureChapterSignatures,
            BoundaryInstruction = boundaryInstruction,
        };
    }

    public static string BuildPlanText(Chapter chapter)
    {
        var parts = new List<string?>();
        if (!string.IsNullOrWhiteSpace(chapter.Title)) parts.Add($"标题：{chapter.Title}");
        if (!string.IsNullOrWhiteSpace(chapter.Goal)) parts.Add($"目标：{chapter.Goal}");
        if (!string.IsNullOrWhiteSpace(chapter.Summary)) parts.Add($"概要：{chapter.Summary}");
        if (!string.IsNullOrWhiteSpace(chapter.Conflict)) parts.Add($"冲突：{chapter.Conflict}");
        if (!string.IsNullOrWhiteSpace(chapter.EmotionCurve)) parts.Add($"情感曲线：{chapter.EmotionCurve}");
        if (chapter.MustIncludePoints is { Count: > 0 })
            parts.Add("必中要点：\n" + string.Join("\n", chapter.MustIncludePoints.Select(p => $"- {p}")));
        return string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static ChapterRevealLevel InferRevealLevel(Chapter chapter)
    {
        var text = BuildPlanText(chapter);

        if (ContainsAny(text, "真相", "揭示", "规则解释", "破解", "收束", "结局", "反转"))
            return ChapterRevealLevel.ResolutionOrReveal;
        if (ContainsAny(text, "战斗", "搏斗", "追逐", "追杀", "袭击", "攻击", "正面冲突", "伤亡", "死亡", "死者"))
            return ChapterRevealLevel.Confrontation;
        if (ContainsAny(
                text,
                "直面", "正面遭遇", "短暂接触", "亲眼看见", "实体鬼", "真正的鬼", "实体", "怪物",
                "看到鬼", "看见鬼", "鬼出现", "鬼登场", "鬼现身", "实体出现", "实体登场",
                "怪物出现", "怪物登场", "直接异常", "确认灵异", "真实存在", "灵异事件发生"))
            return ChapterRevealLevel.DirectAnomaly;
        if (ContainsAny(text, "前兆", "预兆", "征兆", "不安", "错觉", "异响", "信号中断", "广播", "变暗", "警觉", "异常", "诡异", "灵异", "怪异", "身影"))
            return ChapterRevealLevel.ForeshadowOnly;

        return chapter.Number <= 1
            ? ChapterRevealLevel.ForeshadowOnly
            : ChapterRevealLevel.DirectAnomaly;
    }

    private static string BuildBoundaryInstruction(
        Chapter chapter,
        StoryOutline outline,
        ChapterRevealLevel revealLevel,
        IReadOnlyList<string> futureBeats)
    {
        var lines = new List<string>
        {
            $"大纲模式：{outline.Mode}；本章揭示等级：{revealLevel}。",
            "只写当前章节计划中的事件、地点、人物行动和信息量；未在当前章节计划出现的未来事件只能保留，不得当作本章剧情展开。",
        };

        lines.Add(revealLevel switch
        {
            ChapterRevealLevel.DailyOnly => "本章只允许日常和普通人物冲突，不写异常、死亡、战斗、空间切换或规则解释。",
            ChapterRevealLevel.ForeshadowOnly => "本章只允许前兆、错觉、异响、气氛变化和人物警觉；不得确认鬼怪/怪物/实体真实存在，不得写实体登场、空间切换、死亡、追逐、战斗或完整规则解释。",
            ChapterRevealLevel.DirectAnomaly => "本章允许直接异常或短暂接触，但不得升级到未规划的死亡、战斗、完整规则解释或终局揭示。",
            ChapterRevealLevel.Confrontation => "本章允许正面冲突，但不得越过当前计划写终局真相、其他大纲分支或后续章节收束。",
            _ => "本章允许阶段性揭示或收束，但仍不得写当前大纲和当前章节计划外的内容。",
        });

        if (outline.Mode == GenerationMode.Original)
            lines.Add("原创大纲：原著内容不得作为剧情来源；只有用户明确提供的局部参考可作为风格/节奏/情绪参考。");
        else if (outline.Mode == GenerationMode.ContinueFromOriginal)
            lines.Add("原著续写大纲：只在原著结局之后自然接续，不重复原著已有桥段。");
        else if (outline.Mode == GenerationMode.SideStoryFromOriginal)
            lines.Add("支线番外大纲：原著范围只作为设定/关系依据，新增情节围绕番外主题展开。");
        else if (outline.Mode == GenerationMode.ExpandOrRewrite)
            lines.Add("扩写/改写大纲：只有用户指定原著范围可作为剧情来源。");

        if (futureBeats.Count > 0)
        {
            lines.Add($"后续章节保留项共有 {futureBeats.Count} 个；它们只用于保存前验收，不作为本章创作素材。生成时不要猜测、展开或提前揭示任何后续章节内容。");
        }

        return string.Join("\n", lines);
    }

    private static List<string> ExtractAllowedLocations(string planText)
        => LocationMarkers
            .Where(marker => planText.Contains(marker, StringComparison.Ordinal))
            .Distinct()
            .ToList();

    private static string FormatFutureChapter(Chapter chapter)
    {
        var parts = new List<string> { $"第 {chapter.Number} 章《{chapter.Title ?? "未命名"}》" };
        if (!string.IsNullOrWhiteSpace(chapter.Goal)) parts.Add($"目标：{chapter.Goal}");
        if (!string.IsNullOrWhiteSpace(chapter.Summary)) parts.Add($"概要：{chapter.Summary}");
        if (!string.IsNullOrWhiteSpace(chapter.Conflict)) parts.Add($"冲突：{chapter.Conflict}");
        return string.Join("；", parts);
    }

    private static FutureChapterSignature? BuildFutureChapterSignature(Chapter chapter, string currentPlanText)
    {
        var signals = new HashSet<string>(StringComparer.Ordinal);

        AddSignatureSignal(signals, chapter.Title, currentPlanText);

        foreach (var source in EnumerateFutureChapterSources(chapter))
        {
            foreach (var signal in ExtractSignatureSignals(source, currentPlanText))
                signals.Add(signal);
        }

        if (signals.Count == 0)
            return null;

        return new FutureChapterSignature
        {
            ChapterNumber = chapter.Number,
            Title = chapter.Title ?? $"第 {chapter.Number} 章",
            Signals = signals
                .OrderByDescending(signal => signal.Length)
                .Take(8)
                .ToList(),
        };
    }

    private static IEnumerable<string> EnumerateFutureChapterSources(Chapter chapter)
    {
        if (!string.IsNullOrWhiteSpace(chapter.Goal))
            yield return chapter.Goal;
        if (!string.IsNullOrWhiteSpace(chapter.Summary))
            yield return chapter.Summary;
        if (!string.IsNullOrWhiteSpace(chapter.Conflict))
            yield return chapter.Conflict;

        foreach (var point in chapter.MustIncludePoints.Where(point => !string.IsNullOrWhiteSpace(point)))
            yield return point;
    }

    private static IEnumerable<string> ExtractSignatureSignals(string text, string currentPlanText)
    {
        var normalized = text
            .Replace("：", "，")
            .Replace("；", "，")
            .Replace("。", "，")
            .Replace("、", "，")
            .Replace("\n", "，");

        foreach (var raw in normalized.Split('，', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var signal = raw.Trim(' ', '-', '《', '》', '【', '】', '“', '”', '"');
            if (signal.Length is < 3 or > 24)
                continue;
            if (SignatureNoiseTerms.Any(term => signal.Contains(term, StringComparison.Ordinal)))
                continue;
            if (currentPlanText.Contains(signal, StringComparison.Ordinal))
                continue;

            yield return signal;
        }
    }

    private static void AddSignatureSignal(HashSet<string> signals, string? value, string currentPlanText)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var signal = value.Trim();
        if (signal.Length is < 2 or > 20)
            return;
        if (currentPlanText.Contains(signal, StringComparison.Ordinal))
            return;

        signals.Add(signal);
    }

    private static bool ContainsAny(string text, params string[] terms)
        => terms.Any(term => text.Contains(term, StringComparison.Ordinal));

    [GeneratedRegex("[，。；、\\s]+")]
    private static partial Regex SeparatorRegex();
}
