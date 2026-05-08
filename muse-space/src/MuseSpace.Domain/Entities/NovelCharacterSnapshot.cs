namespace MuseSpace.Domain.Entities;

/// <summary>
/// 原著主要角色的结尾状态快照（由 NovelEndingSummaryJob 自动提取）。
/// 每条记录对应一个原著角色在故事结尾时的状态描述。
/// 续写/番外模式下作为"人物末态约束"注入 Prompt，防止 AI 改变人物命运。
/// </summary>
public class NovelCharacterSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>所属原著</summary>
    public Guid NovelId { get; set; }

    /// <summary>冗余存储，方便按项目过滤</summary>
    public Guid StoryProjectId { get; set; }

    /// <summary>角色名称（来自原著，不一定与项目角色库同名）</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>
    /// 角色结尾状态描述，如：
    /// "与女主和解，留守边关，内心得到救赎，未婚"
    /// "死于第30章战役，是不可逆事件"
    /// </summary>
    public string EndingState { get; set; } = string.Empty;

    /// <summary>是否为不可逆状态（死亡 / 永久分离 / 关键转折）</summary>
    public bool IsIrreversible { get; set; }

    /// <summary>
    /// 与项目角色库中已有角色的关联（可选）。
    /// 由用户手动绑定，或后续按名称模糊匹配填充。
    /// </summary>
    public Guid? LinkedCharacterId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
