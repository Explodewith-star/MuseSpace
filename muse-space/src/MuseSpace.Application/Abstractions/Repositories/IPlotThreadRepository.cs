using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IPlotThreadRepository
{
    Task<List<PlotThread>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<PlotThread?> GetByIdAsync(Guid projectId, Guid id, CancellationToken ct = default);
    Task<PlotThread> AddAsync(PlotThread thread, CancellationToken ct = default);
    Task UpdateAsync(PlotThread thread, CancellationToken ct = default);
    Task DeleteAsync(Guid projectId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// 返回对指定批次可见的伏笔列表（依据 Visibility + ChainId/OutlineId 过滤）。
    /// </summary>
    Task<List<PlotThread>> GetVisibleToOutlineAsync(
        Guid projectId,
        Guid outlineId,
        Guid? chainId,
        CancellationToken ct = default);
}

