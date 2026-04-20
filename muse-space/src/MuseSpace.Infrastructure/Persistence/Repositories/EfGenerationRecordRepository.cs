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
}
