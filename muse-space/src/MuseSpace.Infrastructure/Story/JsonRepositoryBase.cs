using System.Text.Encodings.Web;
using System.Text.Json;

namespace MuseSpace.Infrastructure.Story;

/// <summary>
/// JSON 文件仓储的通用基类，封装读写 List&lt;T&gt; 到本地 JSON 文件的逻辑。
/// </summary>
public abstract class JsonRepositoryBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    protected static async Task<List<T>> ReadFileAsync<T>(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return [];
        var json = await File.ReadAllTextAsync(filePath, ct);
        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
    }

    protected static async Task<T?> ReadSingleFileAsync<T>(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return default;
        var json = await File.ReadAllTextAsync(filePath, ct);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    protected static async Task WriteFileAsync<T>(string filePath, List<T> items, CancellationToken ct = default)
    {
        EnsureDirectory(filePath);
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(items, JsonOptions), ct);
    }

    protected static async Task WriteSingleFileAsync<T>(string filePath, T item, CancellationToken ct = default)
    {
        EnsureDirectory(filePath);
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(item, JsonOptions), ct);
    }

    protected static string GetProjectDir(string basePath, Guid projectId)
        => Path.Combine(basePath, "projects", projectId.ToString());

    protected static string GetProjectFilePath(string basePath, Guid projectId, string fileName)
        => Path.Combine(GetProjectDir(basePath, projectId), fileName);

    private static void EnsureDirectory(string filePath)
        => Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
}
