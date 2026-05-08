using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfBackgroundTaskRepository : IBackgroundTaskRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfBackgroundTaskRepository(MuseSpaceDbContext db) => _db = db;

    public async Task<BackgroundTaskRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.BackgroundTasks.FindAsync([id], cancellationToken);

    public async Task<List<BackgroundTaskRecord>> GetByUserAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default)
        => await _db.BackgroundTasks
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<List<BackgroundTaskRecord>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _db.BackgroundTasks
            .Where(t => t.UserId == userId &&
                (t.Status == BackgroundTaskStatus.Pending || t.Status == BackgroundTaskStatus.Running))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(BackgroundTaskRecord record, CancellationToken cancellationToken = default)
    {
        _db.BackgroundTasks.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(BackgroundTaskRecord record, CancellationToken cancellationToken = default)
    {
        _db.BackgroundTasks.Update(record);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
