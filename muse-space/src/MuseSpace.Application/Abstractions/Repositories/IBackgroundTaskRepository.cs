using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IBackgroundTaskRepository
{
    Task<BackgroundTaskRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<BackgroundTaskRecord>> GetByUserAsync(Guid userId, int limit = 50, CancellationToken cancellationToken = default);
    Task<List<BackgroundTaskRecord>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(BackgroundTaskRecord record, CancellationToken cancellationToken = default);
    Task UpdateAsync(BackgroundTaskRecord record, CancellationToken cancellationToken = default);
}
