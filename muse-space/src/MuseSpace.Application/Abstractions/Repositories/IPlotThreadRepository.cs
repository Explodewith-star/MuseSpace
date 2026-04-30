using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IPlotThreadRepository
{
    Task<List<PlotThread>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<PlotThread?> GetByIdAsync(Guid projectId, Guid id, CancellationToken ct = default);
    Task<PlotThread> AddAsync(PlotThread thread, CancellationToken ct = default);
    Task UpdateAsync(PlotThread thread, CancellationToken ct = default);
    Task DeleteAsync(Guid projectId, Guid id, CancellationToken ct = default);
}
