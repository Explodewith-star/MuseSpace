using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Persistence.Repositories;

public sealed class EfPlotThreadRepository : IPlotThreadRepository
{
    private readonly MuseSpaceDbContext _db;
    public EfPlotThreadRepository(MuseSpaceDbContext db) => _db = db;

    public async Task<List<PlotThread>> GetByProjectAsync(Guid projectId, CancellationToken ct = default)
        => await _db.PlotThreads.AsNoTracking()
            .Where(t => t.StoryProjectId == projectId)
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync(ct);

    public Task<PlotThread?> GetByIdAsync(Guid projectId, Guid id, CancellationToken ct = default)
        => _db.PlotThreads.FirstOrDefaultAsync(t => t.StoryProjectId == projectId && t.Id == id, ct);

    public async Task<PlotThread> AddAsync(PlotThread thread, CancellationToken ct = default)
    {
        thread.CreatedAt = thread.UpdatedAt = DateTime.UtcNow;
        _db.PlotThreads.Add(thread);
        await _db.SaveChangesAsync(ct);
        return thread;
    }

    public async Task UpdateAsync(PlotThread thread, CancellationToken ct = default)
    {
        thread.UpdatedAt = DateTime.UtcNow;
        _db.PlotThreads.Update(thread);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid projectId, Guid id, CancellationToken ct = default)
    {
        var item = await _db.PlotThreads.FirstOrDefaultAsync(
            t => t.StoryProjectId == projectId && t.Id == id, ct);
        if (item is null) return;
        _db.PlotThreads.Remove(item);
        await _db.SaveChangesAsync(ct);
    }
}
