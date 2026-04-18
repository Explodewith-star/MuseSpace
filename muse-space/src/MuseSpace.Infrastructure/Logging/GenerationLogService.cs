using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Logging;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Logging;

public sealed class GenerationLogService : IGenerationLogService
{
    private readonly ILogger<GenerationLogService> _logger;
    private readonly string _logDirectory;

    public GenerationLogService(ILogger<GenerationLogService> logger, string logDirectory)
    {
        _logger = logger;
        _logDirectory = logDirectory;
    }

    public async Task LogAsync(GenerationRecord record, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Generation] RequestId={RequestId} Skill={SkillName} Prompt={PromptName} " +
            "Duration={DurationMs}ms Success={Success}",
            record.RequestId,
            record.SkillName,
            record.PromptName,
            record.DurationMs,
            record.Success);

        Directory.CreateDirectory(_logDirectory);

        var fileName = $"{record.CreatedAt:yyyyMMdd_HHmmss}_{record.RequestId}.json";
        var filePath = Path.Combine(_logDirectory, fileName);

        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
}

//主角第二天出门准备继续寻找妹妹的线索，正巧遇到了方晴，主角表达了感激后，和方晴寒暄了几句，便离开继续寻找妹妹的线索。

//    主角遇到女警后表达了感谢。

//    坚定-惊讶-愉快
