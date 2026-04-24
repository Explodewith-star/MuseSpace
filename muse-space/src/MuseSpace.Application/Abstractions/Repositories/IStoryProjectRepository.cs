using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Repositories;

public interface IStoryProjectRepository
{
    Task<StoryProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<StoryProject>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>userId=null 返回游客共享项目；有值返回该用户私有项目</summary>
    Task<List<StoryProject>> GetByUserIdAsync(Guid? userId, CancellationToken cancellationToken = default);
    Task SaveAsync(StoryProject project, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
