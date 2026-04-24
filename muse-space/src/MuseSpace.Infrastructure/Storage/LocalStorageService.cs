using Microsoft.Extensions.Options;
using MuseSpace.Application.Abstractions.Storage;
using MuseSpace.Infrastructure.Story;

namespace MuseSpace.Infrastructure.Storage;

/// <summary>
/// 基于本地文件系统的存储实现。开发阶段使用，后续可替换为 COS/MinIO。
/// 文件根目录：{DataOptions.BasePath}/files/
/// </summary>
public sealed class LocalStorageService : IStorageService
{
    private readonly string _basePath;

    public LocalStorageService(IOptions<DataOptions> options)
    {
        _basePath = Path.Combine(options.Value.BasePath, "files");
        Directory.CreateDirectory(Path.Combine(_basePath, "raw"));
    }

    public async Task SaveAsync(string key, Stream content, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var fs = File.Create(fullPath);
        await content.CopyToAsync(fs, ct);
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(key);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {key}", fullPath);
        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(File.Exists(GetFullPath(key)));

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(key);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string GetFullPath(string key)
    {
        // Sanitize to prevent path traversal
        var safeParts = key.Split('/', '\\')
            .Where(p => !string.IsNullOrEmpty(p) && p != ".." && p != ".")
            .ToArray();
        return Path.Combine([_basePath, .. safeParts]);
    }
}
