namespace MuseSpace.Application.Abstractions.Skills;

public class SkillRequest
{
    /// <summary>
    /// 任务类型标识，ISkillOrchestrator 依据此值路由到具体 Skill。
    /// 每个 ISkill 实现通过 <see cref="ISkill.TaskType"/> 声明自己能处理的值。
    /// </summary>
    public string TaskType { get; init; } = string.Empty;

    public Guid StoryProjectId { get; init; }

    /// <summary>Skill 所需的附加参数，各 Skill 自行约定键名。</summary>
    public Dictionary<string, string> Parameters { get; init; } = [];
}
