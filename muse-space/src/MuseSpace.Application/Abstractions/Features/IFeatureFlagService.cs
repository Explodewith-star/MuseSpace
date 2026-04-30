using MuseSpace.Domain.Entities;

namespace MuseSpace.Application.Abstractions.Features;

/// <summary>
/// 内置 Feature Flag Key 常量。所有自动链路类 Job 都应通过这些 key 控制开/关。
/// </summary>
public static class FeatureFlagKeys
{
    /// <summary>草稿生成后是否自动跑角色一致性检查。</summary>
    public const string AutoCharacterConsistency = "auto-character-consistency";

    /// <summary>草稿生成后是否自动扫描伏笔线索。</summary>
    public const string AutoPlotThreadTracking = "auto-plot-thread-tracking";

    /// <summary>原著导入后是否自动提取候选资产（角色/世界观/文风）。</summary>
    public const string AutoExtractNovelAssets = "auto-extract-novel-assets";
}

/// <summary>
/// 功能开关服务。读取带本地缓存（30 秒），写入立即失效缓存。
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>查询某 flag 是否开启；不存在时返回 defaultValue。</summary>
    Task<bool> IsEnabledAsync(string key, bool defaultValue = false, CancellationToken ct = default);

    /// <summary>列出全部 flag。</summary>
    Task<List<FeatureFlag>> ListAsync(CancellationToken ct = default);

    /// <summary>开启或关闭某 flag；不存在则创建。</summary>
    Task UpsertAsync(string key, bool isEnabled, string? description = null, CancellationToken ct = default);
}
