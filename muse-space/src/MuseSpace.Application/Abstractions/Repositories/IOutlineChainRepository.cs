using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IOutlineChainRepository
{
    Task<List<OutlineChain>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<OutlineChain?> GetByIdAsync(Guid chainId, CancellationToken cancellationToken = default);
    Task SaveAsync(OutlineChain chain, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid chainId, CancellationToken cancellationToken = default);
}
