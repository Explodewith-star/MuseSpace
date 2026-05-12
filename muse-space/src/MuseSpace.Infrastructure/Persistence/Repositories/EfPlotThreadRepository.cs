using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

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

    /// <inheritdoc/>
    public async Task<List<PlotThread>> GetVisibleToOutlineAsync(
        Guid projectId,
        Guid outlineId,
        Guid? chainId,
        CancellationToken ct = default)
    {
        return await _db.PlotThreads.AsNoTracking()
            .Where(t => t.StoryProjectId == projectId
                // 忽略已回收/废弃的伏笔
                && t.Status != ForeshadowingStatus.PaidOff
                && t.Status != ForeshadowingStatus.Abandoned
                && (
                    // Project 级：全项目可见
                    t.Visibility == PlotThreadVisibility.Project
                    // Chain 级：同一故事链可见（chainId 匹配，或历史数据 ChainId 为 null 时宽松放行）
                    || (t.Visibility == PlotThreadVisibility.Chain
                        && (chainId == null || t.ChainId == null || t.ChainId == chainId))
                    // ThisOutline 级：仅限埋设批次
                    || (t.Visibility == PlotThreadVisibility.ThisOutline && t.OutlineId == outlineId)
                    // 历史数据无 Visibility 字段（OutlineId/ChainId 均为 null）：视同 Project 级，不过滤
                    || (t.OutlineId == null && t.ChainId == null)
                ))
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync(ct);
    }
}

