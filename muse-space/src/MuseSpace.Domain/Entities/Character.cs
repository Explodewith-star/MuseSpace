namespace MuseSpace.Domain.Entities;

public class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryProjectId { get; set; }

    /// <summary>
    /// 归属的大纲 ID。
    /// null = 原著角色池（项目级只读参考，由原著导入自动填充，不属于任何大纲）。
    /// 有值 = 已归属到某个具体大纲，各大纲独立隔离。
    /// </summary>
    public Guid? StoryOutlineId { get; set; }

    /// <summary>从哪部原著提取，null 表示原创手动添加。</summary>
    public Guid? SourceNovelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Age { get; set; }

    /// <summary>身份定位：主角/配角/反派/龙套/其他</summary>
    public string? Role { get; set; }
    public string? PersonalitySummary { get; set; }
    public string? Motivation { get; set; }
    public string? SpeakingStyle { get; set; }
    public string? ForbiddenBehaviors { get; set; }
    public string? PublicSecrets { get; set; }
    public string? PrivateSecrets { get; set; }
    public string? CurrentState { get; set; }
    public string? Tags { get; set; }
}
