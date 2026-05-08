namespace MuseSpace.Contracts.Suggestions;

/// <summary>建议类型常量，用于 AgentSuggestion.Category。</summary>
public static class SuggestionCategories
{
    // ── 资产类（Apply 时落业务表） ──────────────────────────────
    public const string Character = "Character";
    public const string WorldRule = "WorldRule";
    public const string Outline = "Outline";
    public const string StyleProfile = "StyleProfile";

    // ── 一致性类（细分；Apply 仅"知晓"，不落表） ───────────────
    public const string WorldRuleConsistency = "WorldRuleConsistency";
    public const string CharacterConsistency = "CharacterConsistency";
    public const string StyleConsistency = "StyleConsistency";
    public const string OutlineConsistency = "OutlineConsistency";

    // ── 其它通知类 ──────────────────────────────────────────────
    public const string ProjectSummary = "ProjectSummary";
    public const string PlotThread = "PlotThread";

    // ── Module D 正典事实层 ─────────────────────────────────────
    public const string CanonEvent = "CanonEvent";
    public const string CanonFact = "CanonFact";

    /// <summary>
    /// 历史遗留的总一致性类目；新代码不再写入。仅用于兼容老数据查询。
    /// 启动时迁移会按 title 前缀拆分到 4 个 *Consistency 子类目。
    /// </summary>
    [Obsolete("使用具体的 *Consistency 类目")]
    public const string Consistency = "Consistency";
}

