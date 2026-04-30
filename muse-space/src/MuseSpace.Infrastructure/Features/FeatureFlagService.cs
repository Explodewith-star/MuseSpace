using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MuseSpace.Application.Abstractions.Features;
using MuseSpace.Domain.Entities;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Infrastructure.Features;

/// <summary>
/// <see cref="IFeatureFlagService"/> 的 DB-backed 实现。
/// 用 IMemoryCache 做 30 秒短缓存，避免热点 flag 每次请求都查 DB。
/// </summary>
public sealed class FeatureFlagService : IFeatureFlagService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private readonly MuseSpaceDbContext _db;
    private readonly IMemoryCache _cache;

    public FeatureFlagService(MuseSpaceDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    private static string CacheKey(string key) => $"feature-flag:{key}";

    public async Task<bool> IsEnabledAsync(string key, bool defaultValue = false, CancellationToken ct = default)
    {
        if (_cache.TryGetValue<bool?>(CacheKey(key), out var cached) && cached.HasValue)
            return cached.Value;

        var item = await _db.FeatureFlags.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Key == key, ct);
        var value = item?.IsEnabled ?? defaultValue;
        _cache.Set(CacheKey(key), (bool?)value, CacheTtl);
        return value;
    }

    public Task<List<FeatureFlag>> ListAsync(CancellationToken ct = default)
        => _db.FeatureFlags.AsNoTracking().OrderBy(f => f.Key).ToListAsync(ct);

    public async Task UpsertAsync(string key, bool isEnabled, string? description = null, CancellationToken ct = default)
    {
        var item = await _db.FeatureFlags.FirstOrDefaultAsync(f => f.Key == key, ct);
        if (item is null)
        {
            _db.FeatureFlags.Add(new FeatureFlag
            {
                Key = key,
                IsEnabled = isEnabled,
                Description = description,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            item.IsEnabled = isEnabled;
            if (description is not null) item.Description = description;
            item.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey(key));
    }
}
