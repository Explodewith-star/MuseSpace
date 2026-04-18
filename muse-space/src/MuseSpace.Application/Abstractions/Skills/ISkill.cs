namespace MuseSpace.Application.Abstractions.Skills;

/// <summary>
/// 单个创作能力模块的接口。每个 Skill 对应一类创作任务，例如起稿、改稿、一致性检查。
/// </summary>
public interface ISkill
{
    /// <summary>Skill 的可读名称，用于日志标识。</summary>
    string Name { get; }

    /// <summary>
    /// 任务类型标识，ISkillOrchestrator 通过此值路由到对应的 Skill。
    /// 命名约定：全小写 + 连字符，例如 "scene-draft"、"revision"。
    /// </summary>
    string TaskType { get; }

    Task<SkillResult> ExecuteAsync(SkillRequest request, CancellationToken cancellationToken = default);
}
