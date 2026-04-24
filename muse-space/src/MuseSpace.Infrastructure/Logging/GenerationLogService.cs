using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Logging;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Logging;

/// <summary>
/// 生成记录日志服务：仅通过 Serilog 结构化日志记录，不再写入 JSON 文件。
/// </summary>
public sealed class GenerationLogService : IGenerationLogService
{
    private readonly ILogger<GenerationLogService> _logger;

    public GenerationLogService(ILogger<GenerationLogService> logger)
    {
        _logger = logger;
    }

    public Task LogAsync(GenerationRecord record, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Generation] RequestId={RequestId} Skill={SkillName} Prompt={PromptName} " +
            "Duration={DurationMs}ms Success={Success}",
            record.RequestId,
            record.SkillName,
            record.PromptName,
            record.DurationMs,
            record.Success);

        return Task.CompletedTask;
    }
}
