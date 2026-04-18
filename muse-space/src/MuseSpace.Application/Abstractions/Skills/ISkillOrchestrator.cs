namespace MuseSpace.Application.Abstractions.Skills;

/// <summary>
/// Skill 的统一调度入口。Application Service 只与此接口交互，不直接依赖具体 Skill 实现。
/// 内部根据 <see cref="SkillRequest.TaskType"/> 路由到对应的 <see cref="ISkill"/>。
/// </summary>
public interface ISkillOrchestrator
{
    Task<SkillResult> ExecuteAsync(SkillRequest request, CancellationToken cancellationToken = default);
}
