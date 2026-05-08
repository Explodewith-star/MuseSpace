using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfGenerationRecordRepository : IGenerationRecordRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfGenerationRecordRepository(MuseSpaceDbContext db) => _db = db;

    public async Task AddAsync(GenerationRecord record, CancellationToken cancellationToken = default)
    {
        _db.GenerationRecords.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<GenerationRecord>> GetByProjectAsync(
        Guid projectId,
        int limit = 50,
        CancellationToken cancellationToken = default)
        => await _db.GenerationRecords
                    .Where(r => r.StoryProjectId == projectId)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(limit)
                    .ToListAsync(cancellationToken);

    public async Task<ProjectGenerationStats> GetProjectStatsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var records = _db.GenerationRecords
            .Where(r => r.StoryProjectId == projectId);

        var totalCalls = await records.CountAsync(cancellationToken);
        if (totalCalls == 0)
            return new ProjectGenerationStats();

        var succeeded = await records.CountAsync(r => r.Success, cancellationToken);

        var aggregation = await records
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalInputTokens = g.Sum(r => r.InputTokens),
                TotalOutputTokens = g.Sum(r => r.OutputTokens),
                TotalTokens = g.Sum(r => r.TotalTokens),
                TotalDurationMs = g.Sum(r => r.DurationMs),
                AvgDurationMs = g.Average(r => (double)r.DurationMs)
            })
            .FirstAsync(cancellationToken);

        return new ProjectGenerationStats
        {
            TotalCalls = totalCalls,
            SucceededCalls = succeeded,
            FailedCalls = totalCalls - succeeded,
            TotalInputTokens = aggregation.TotalInputTokens,
            TotalOutputTokens = aggregation.TotalOutputTokens,
            TotalTokens = aggregation.TotalTokens,
            TotalDurationMs = aggregation.TotalDurationMs,
            AvgDurationMs = aggregation.AvgDurationMs
        };
    }
}
