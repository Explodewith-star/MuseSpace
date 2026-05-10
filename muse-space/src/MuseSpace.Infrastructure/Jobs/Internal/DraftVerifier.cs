using MuseSpace.Domain.Enums;

namespace MuseSpace.Infrastructure.Jobs.Internal;

public enum DraftViolationSeverity
{
    Warning = 0,
    Blocker = 1,
}

public enum DraftViolationType
{
    FutureBeatLeak,
    RevealLevelExceeded,
    SourcePolicyViolation,
    MissingRequiredBeat,
    OutOfPlanElement,
}

public sealed record DraftViolation(
    DraftViolationType Type,
    DraftViolationSeverity Severity,
    string Evidence,
    string Expected);

public sealed class DraftVerificationResult
{
    public bool IsPassed => Violations.All(v => v.Severity != DraftViolationSeverity.Blocker);
    public List<DraftViolation> Violations { get; init; } = [];

    public string RevisionInstruction
    {
        get
        {
            if (Violations.Count == 0) return string.Empty;
            var lines = new List<string>
            {
                "保存前验收未通过，请完全重写当前章节，并严格遵守以下边界："
            };
            lines.AddRange(Violations
                .Where(v => v.Severity == DraftViolationSeverity.Blocker)
                .Select(v => $"- [{v.Type}] 草稿证据：{v.Evidence}；期望：{v.Expected}"));
            return string.Join("\n", lines);
        }
    }

    public string RevisionInstructionForPrompt
    {
        get
        {
            var blockers = Violations.Where(v => v.Severity == DraftViolationSeverity.Blocker).ToList();
            if (blockers.Count == 0) return string.Empty;

            var lines = new List<string>
            {
                "上一版草稿未通过保存前验收。请完全重写当前章节，只写当前章节计划允许的内容。",
                "不要复用上一版草稿中的地点、事件、实体、战斗、死亡、规则解释或后续章节信息。",
            };

            if (blockers.Any(v => v.Type == DraftViolationType.FutureBeatLeak))
                lines.Add("- 发现提前使用后续章节保留内容；本次重写只能保留为模糊前兆，不得展开后续桥段。");
            if (blockers.Any(v => v.Type == DraftViolationType.RevealLevelExceeded))
                lines.Add("- 发现揭示等级超限；本次重写必须降低信息量，只写错觉、异响、气氛变化和人物警觉。");
            if (blockers.Any(v => v.Type == DraftViolationType.SourcePolicyViolation))
                lines.Add("- 发现来源策略违规；本次重写不得把原著或参考文本当作剧情来源。");
            if (blockers.Any(v => v.Type == DraftViolationType.OutOfPlanElement))
                lines.Add("- 发现当前章节计划外的具体实体、地点、规则或升级桥段；本次重写只能使用章节计划中明确出现的元素。");

            return string.Join("\n", lines);
        }
    }
}

public static class DraftVerifier
{
    private static readonly string[] CommonStopTerms =
    [
        "一个", "一种", "自己", "他们", "我们", "已经", "开始", "突然", "声音",
        "时候", "地方", "事情", "感觉", "没有", "不是", "出现", "发现"
    ];

    private static readonly string[] AbnormalTerms =
    [
        "异常", "诡异", "灵异", "鬼", "鬼眼", "鬼影", "鬼附身", "附身", "怪物", "实体",
        "影子人", "异空间", "鬼域", "门后世界"
    ];

    private static readonly string[] DirectRevealTerms =
    [
        "实体鬼", "真正的鬼", "鬼出现", "鬼登场", "鬼现身", "看见鬼", "看到鬼",
        "鬼眼", "鬼影", "鬼附身", "附身", "被鬼附身", "被附身", "影子人", "人影轮廓", "人形轮廓",
        "鬼域", "异空间", "门后世界", "地下室", "地窖",
        "完整规则", "规则解释", "生存规则", "杀人规则", "杀人规律", "真相", "祭坛", "尸体", "遗体",
        "死亡", "死了", "追杀", "追逐", "实体出现", "实体登场", "怪物出现", "怪物登场",
        "战斗", "搏斗", "袭击", "攻击", "伤亡", "红色液体", "血色", "鲜血", "血管", "猩红", "黑红"
    ];

    private static readonly string[] EscalationTerms =
    [
        "死亡", "死了", "尸体", "遗体", "伤亡", "追杀", "追逐", "战斗", "搏斗", "袭击", "攻击",
        "完整规则", "规则解释", "生存规则", "杀人规则", "真相", "终局", "结局",
        "附身", "鬼附身", "被控制", "控制身体", "力量控制", "强制抉择"
    ];

    private static readonly string[] OutOfPlanTerms =
    [
        "鬼眼", "鬼影", "鬼附身", "附身", "被鬼附身", "被附身", "影子人", "人形轮廓", "实体鬼", "真正的鬼",
        "鬼域", "异空间", "门后世界", "地下室", "地窖", "祭坛", "密室", "实验室",
        "尸体", "遗体", "死亡", "死了", "追杀", "追逐", "战斗", "搏斗", "袭击", "攻击",
        "完整规则", "规则解释", "生存规则", "杀人规则", "杀人规律", "真相", "终局",
        "红色液体", "血色", "鲜血", "血管", "猩红", "黑红", "倒计时", "选一个", "做选择", "强制抉择",
        "被控制", "控制身体", "力量控制", "历史的一员", "保命的底牌"
    ];

    private static readonly string[] ConcreteFutureTerms =
    [
        "实体鬼", "真正的鬼", "鬼域", "异空间", "门后世界", "地下室", "地窖",
        "祭坛", "尸体", "遗体", "密室", "实验室"
    ];

    private static readonly string[] GenericFutureTerms =
    [
        "鬼", "灵异", "异常", "诡异", "怪异", "怪物", "实体", "规则", "规律", "真相",
        "前兆", "预兆", "征兆", "线索", "秘密", "危险", "恐惧", "广播", "杂音", "信号"
    ];

    public static DraftVerificationResult Verify(ChapterDraftScope scope, string draftText)
    {
        var violations = new List<DraftViolation>();
        if (string.IsNullOrWhiteSpace(draftText))
        {
            violations.Add(new DraftViolation(
                DraftViolationType.MissingRequiredBeat,
                DraftViolationSeverity.Blocker,
                "草稿为空",
                "必须生成当前章节正文"));
            return new DraftVerificationResult { Violations = violations };
        }

        AddRevealViolations(scope, draftText, violations);
        AddOutOfPlanElementViolations(scope, draftText, violations);
        AddFutureBeatViolations(scope, draftText, violations);
        AddSourcePolicyViolations(scope, draftText, violations);
        AddMissingBeatWarnings(scope, draftText, violations);

        return new DraftVerificationResult
        {
            Violations = violations
                .DistinctBy(v => new { v.Type, v.Evidence })
                .Take(12)
                .ToList(),
        };
    }

    private static void AddRevealViolations(
        ChapterDraftScope scope,
        string draftText,
        List<DraftViolation> violations)
    {
        var currentPlan = scope.CurrentPlanText;
        var terms = scope.AllowedRevealLevel switch
        {
            ChapterRevealLevel.DailyOnly => AbnormalTerms.Concat(DirectRevealTerms),
            ChapterRevealLevel.ForeshadowOnly => DirectRevealTerms,
            ChapterRevealLevel.DirectAnomaly => EscalationTerms,
            _ => []
        };

        var hits = terms
            .Where(term => draftText.Contains(term, StringComparison.Ordinal)
                && !currentPlan.Contains(term, StringComparison.Ordinal))
            .Distinct()
            .Take(6)
            .ToList();

        if (hits.Count == 0) return;

        violations.Add(new DraftViolation(
            DraftViolationType.RevealLevelExceeded,
            DraftViolationSeverity.Blocker,
            string.Join("、", hits),
            $"本章揭示等级为 {scope.AllowedRevealLevel}，只能写到当前章节计划允许的信息量"));
    }

    private static void AddFutureBeatViolations(
        ChapterDraftScope scope,
        string draftText,
        List<DraftViolation> violations)
    {
        var futureText = string.Join("\n", scope.ReservedFutureBeats);
        if (string.IsNullOrWhiteSpace(futureText)) return;

        var futureTerms = ExtractReservedTerms(futureText, scope.CurrentPlanText, includeConcreteFutureTerms: true)
            .Where(term => draftText.Contains(term, StringComparison.Ordinal))
            .Take(8)
            .ToList();

        if (futureTerms.Count == 0) return;

        violations.Add(new DraftViolation(
            DraftViolationType.FutureBeatLeak,
            DraftViolationSeverity.Blocker,
            string.Join("、", futureTerms),
            "这些内容属于后续章节保留项，本章只能推进当前章节计划"));
    }

    private static void AddOutOfPlanElementViolations(
        ChapterDraftScope scope,
        string draftText,
        List<DraftViolation> violations)
    {
        if (scope.AllowedRevealLevel >= ChapterRevealLevel.ResolutionOrReveal)
            return;

        var currentPlan = scope.CurrentPlanText;
        var hits = OutOfPlanTerms
            .Where(term => draftText.Contains(term, StringComparison.Ordinal)
                && !currentPlan.Contains(term, StringComparison.Ordinal))
            .ToList();

        if (scope.AllowedRevealLevel == ChapterRevealLevel.DailyOnly)
        {
            hits.AddRange(AbnormalTerms
                .Where(term => draftText.Contains(term, StringComparison.Ordinal)
                    && !currentPlan.Contains(term, StringComparison.Ordinal)));
        }

        if (scope.AllowedRevealLevel == ChapterRevealLevel.ForeshadowOnly)
        {
            hits.AddRange(DirectRevealTerms
                .Where(term => draftText.Contains(term, StringComparison.Ordinal)
                    && !currentPlan.Contains(term, StringComparison.Ordinal)));
        }

        if (scope.AllowedLocations.Count > 0)
        {
            hits.AddRange(ConcreteFutureTerms
                .Where(term => draftText.Contains(term, StringComparison.Ordinal)
                    && !currentPlan.Contains(term, StringComparison.Ordinal)
                    && !scope.AllowedLocations.Contains(term, StringComparer.Ordinal)));
        }

        hits = hits
            .Distinct()
            .Take(8)
            .ToList();

        if (hits.Count == 0) return;

        violations.Add(new DraftViolation(
            DraftViolationType.OutOfPlanElement,
            DraftViolationSeverity.Blocker,
            string.Join("、", hits),
            "这些具体元素未出现在当前章节计划中，不得在本章展开"));
    }

    private static void AddSourcePolicyViolations(
        ChapterDraftScope scope,
        string draftText,
        List<DraftViolation> violations)
    {
        if (scope.GenerationMode != GenerationMode.Original)
            return;

        var sourceSignals = new[] { "原著中", "原文里", "按照原著", "照着原著", "复述原著" };
        var hits = sourceSignals
            .Where(term => draftText.Contains(term, StringComparison.Ordinal))
            .ToList();
        if (hits.Count == 0) return;

        violations.Add(new DraftViolation(
            DraftViolationType.SourcePolicyViolation,
            DraftViolationSeverity.Blocker,
            string.Join("、", hits),
            "原创大纲不能把原著作为剧情来源"));
    }

    private static void AddMissingBeatWarnings(
        ChapterDraftScope scope,
        string draftText,
        List<DraftViolation> violations)
    {
        foreach (var beat in scope.RequiredBeats.Take(8))
        {
            var keyTerms = ExtractReservedTerms(beat, string.Empty, includeConcreteFutureTerms: false).Take(3).ToList();
            if (keyTerms.Count == 0) continue;
            if (keyTerms.Any(term => draftText.Contains(term, StringComparison.Ordinal))) continue;

            violations.Add(new DraftViolation(
                DraftViolationType.MissingRequiredBeat,
                DraftViolationSeverity.Warning,
                beat,
                "必中要点应尽量在草稿中体现"));
        }
    }

    private static IEnumerable<string> ExtractReservedTerms(
        string text,
        string currentPlanText,
        bool includeConcreteFutureTerms)
    {
        var normalized = text
            .Replace("：", "，")
            .Replace("；", "，")
            .Replace("。", "，")
            .Replace("、", "，")
            .Replace("\n", "，");

        foreach (var raw in normalized.Split('，', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var term = raw.Trim(' ', '-', '《', '》', '【', '】');
            if (term.Length is >= 3 and <= 24
                && !CommonStopTerms.Any(stop => term.Contains(stop, StringComparison.Ordinal))
                && (!includeConcreteFutureTerms || !IsLowSignalFutureTerm(term))
                && !currentPlanText.Contains(term, StringComparison.Ordinal))
            {
                yield return term;
            }
        }

        if (!includeConcreteFutureTerms) yield break;

        foreach (var term in ConcreteFutureTerms.Distinct())
        {
            if (text.Contains(term, StringComparison.Ordinal)
                && !currentPlanText.Contains(term, StringComparison.Ordinal))
                yield return term;
        }
    }

    private static bool IsLowSignalFutureTerm(string term)
    {
        if (GenericFutureTerms.Contains(term, StringComparer.Ordinal))
            return true;

        return term.Length <= 4
            && GenericFutureTerms.Any(generic => term.Contains(generic, StringComparison.Ordinal));
    }
}
