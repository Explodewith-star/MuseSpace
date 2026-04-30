using System.Text.Json;
using System.Text.RegularExpressions;

namespace MuseSpace.Infrastructure.Jobs.Internal;

/// <summary>
/// 统一处理 LLM 返回 JSON 的常见污染场景：
/// 1) ```json ... ``` 围栏；2) ``` 围栏；3) 前后多余的换行/反引号。
/// </summary>
public static partial class LlmJsonExtractor
{
    [GeneratedRegex(@"```\w*\n?", RegexOptions.None)]
    private static partial Regex FencePrefixRegex();

    /// <summary>剥掉 markdown 代码围栏与首尾空白。</summary>
    public static string Clean(string raw)
    {
        var json = (raw ?? string.Empty).Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            json = FencePrefixRegex().Replace(json, string.Empty).Trim('`').Trim();
        }
        return json;
    }

    /// <summary>
    /// 清洗后反序列化为 T；解析失败返回 default(T)，不抛异常。调用方应判 null。
    /// </summary>
    public static T? TryDeserialize<T>(string raw, JsonSerializerOptions? opts = null) where T : class
    {
        var json = Clean(raw);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            opts ??= new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<T>(json, opts);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
