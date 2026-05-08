using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IGenerationRecordRepository
{
    Task AddAsync(GenerationRecord record, CancellationToken cancellationToken = default);

    Task<List<GenerationRecord>> GetByProjectAsync(
        Guid projectId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<ProjectGenerationStats> GetProjectStatsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}

public sealed class ProjectGenerationStats
{
    public int TotalCalls { get; init; }
    public int SucceededCalls { get; init; }
    public int FailedCalls { get; init; }
    public int TotalInputTokens { get; init; }
    public int TotalOutputTokens { get; init; }
    public int TotalTokens { get; init; }
    public long TotalDurationMs { get; init; }
    public double AvgDurationMs { get; init; }
}
