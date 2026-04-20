using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IGenerationRecordRepository
{
    Task AddAsync(GenerationRecord record, CancellationToken cancellationToken = default);

    Task<List<GenerationRecord>> GetByProjectAsync(
        Guid projectId,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
