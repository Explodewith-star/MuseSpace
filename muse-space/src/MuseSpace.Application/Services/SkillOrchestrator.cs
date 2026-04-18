using MuseSpace.Application.Abstractions.Skills;

namespace MuseSpace.Application.Services;

public sealed class SkillOrchestrator : ISkillOrchestrator
{
    private readonly Dictionary<string, ISkill> _skills;

    public SkillOrchestrator(IEnumerable<ISkill> skills)
    {
        // DI 容器会将所有注册为 ISkill 的实现一次性注入。
        // 这里以 TaskType 为 key 建立路由表，新增 Skill 只需注册到 DI，无需修改此处。
        _skills = skills.ToDictionary(s => s.TaskType, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<SkillResult> ExecuteAsync(SkillRequest request, CancellationToken cancellationToken = default)
    {
        if (!_skills.TryGetValue(request.TaskType, out var skill))
        {
            return new SkillResult
            {
                Success = false,
                Output = string.Empty,
                ErrorMessage = $"No skill registered for task type: {request.TaskType}",
                SkillName = "unknown"
            };
        }

        return await skill.ExecuteAsync(request, cancellationToken);
    }
}
