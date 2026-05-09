namespace MuseSpace.Domain.Entities;

/// <summary>
/// 固定事实（Module D 正典事实层 / 第二层：状态真相）。
/// 用于记录"已经成为既定事实"的内容，CanonConflictCheckJob 会在生成后比对本表，
/// 若新章节否认 / 反转 / 遗漏已锁定事实则产出 Blocking 级建议。
/// </summary>
public class CanonFact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryProjectId { get; set; }
    public Guid StoryOutlineId { get; set; }

    /// <summary>事实类型：Relationship / Identity / LifeStatus / WorldState / UniqueEvent。</summary>
    public string FactType { get; set; } = string.Empty;

    /// <summary>事实主体（通常是角色 ID；世界事实可为 null）。</summary>
    public Guid? SubjectId { get; set; }

    /// <summary>事实客体（多用于关系类，如 SubjectId 与 ObjectId 已订婚）。</summary>
    public Guid? ObjectId { get; set; }

    /// <summary>
    /// 事实 Key，需要在 (ProjectId, FactType, FactKey) 维度具备语义唯一性，
    /// 例如 "Relationship:abc-def"、"Identity:abc"、"UniqueEvent:proposal:abc-def"。
    /// </summary>
    public string FactKey { get; set; } = string.Empty;

    /// <summary>事实当前取值，如 "Engaged" / "Exposed" / "Alive" / "Happened"。</summary>
    public string FactValue { get; set; } = string.Empty;

    /// <summary>事实首次成立的章节（手动创建时可为空）。</summary>
    public Guid? SourceChapterId { get; set; }

    /// <summary>置信度（0-1，AI 抽取时填，手动创建时为 1）。</summary>
    public double Confidence { get; set; } = 1.0;

    /// <summary>
    /// 是否已锁定。锁定后 CanonConflictCheckJob 视该事实为"不可被推翻"，
    /// 任何后续章节生成与之矛盾则产出 Blocking 级建议。
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>事实被推翻的章节（如已订婚 → 第八章退婚则填入）。</summary>
    public Guid? InvalidatedByChapterId { get; set; }

    /// <summary>备注 / 出处片段。</summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
