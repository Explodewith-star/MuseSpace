using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Logging;

public interface IGenerationLogService
{
    Task LogAsync(GenerationRecord record, CancellationToken cancellationToken = default);
}
