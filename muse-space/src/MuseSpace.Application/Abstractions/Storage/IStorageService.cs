namespace MuseSpace.Application.Abstractions.Storage;

/// <summary>
/// 文件存储抽象。开发阶段底层为本地文件系统；后续可替换为 COS/MinIO。
/// </summary>
public interface IStorageService
{
    Task SaveAsync(string key, Stream content, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
