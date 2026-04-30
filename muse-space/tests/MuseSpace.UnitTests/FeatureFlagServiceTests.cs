using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MuseSpace.Application.Abstractions.Features;
using MuseSpace.Domain.Entities;
using MuseSpace.Infrastructure.Features;
using MuseSpace.Infrastructure.Persistence;
using MuseSpace.Infrastructure.Persistence.Entities;

namespace MuseSpace.UnitTests;

/// <summary>
/// 测试用 DbContext：忽略 pgvector 的 Vector 列，避免 InMemory provider 报"不支持的类型"。
/// </summary>
file sealed class TestDbContext : MuseSpaceDbContext
{
    public TestDbContext(DbContextOptions<MuseSpaceDbContext> opts) : base(opts) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<NovelChunkEmbedding>().Ignore(e => e.Embedding);
    }
}

public sealed class FeatureFlagServiceTests
{
    private static MuseSpaceDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<MuseSpaceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestDbContext(opts);
    }

    [Fact]
    public async Task IsEnabled_ReturnsDefaultValue_WhenKeyNotExists()
    {
        await using var db = NewDb();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new FeatureFlagService(db, cache);

        Assert.True(await svc.IsEnabledAsync("missing-key", defaultValue: true));
        Assert.False(await svc.IsEnabledAsync("missing-key-2", defaultValue: false));
    }

    [Fact]
    public async Task Upsert_ThenIsEnabled_ReflectsValueAndInvalidatesCache()
    {
        await using var db = NewDb();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new FeatureFlagService(db, cache);

        // 第一次读：未命中默认 false
        Assert.False(await svc.IsEnabledAsync("k1"));

        // 写入开启
        await svc.UpsertAsync("k1", isEnabled: true, description: "test");
        Assert.True(await svc.IsEnabledAsync("k1"));

        // 写入关闭后立刻读应反映新值（缓存被失效）
        await svc.UpsertAsync("k1", isEnabled: false);
        Assert.False(await svc.IsEnabledAsync("k1"));
    }

    [Fact]
    public async Task List_ReturnsAllFlagsOrderedByKey()
    {
        await using var db = NewDb();
        var svc = new FeatureFlagService(db, new MemoryCache(new MemoryCacheOptions()));

        await svc.UpsertAsync("b-key", true);
        await svc.UpsertAsync("a-key", false);
        await svc.UpsertAsync("c-key", true);

        var list = await svc.ListAsync();
        Assert.Equal(["a-key", "b-key", "c-key"], list.Select(f => f.Key).ToArray());
    }

    [Fact]
    public void FeatureFlagKeys_AreNonEmptyConstants()
    {
        // 防止常量被误删
        Assert.False(string.IsNullOrWhiteSpace(FeatureFlagKeys.AutoCharacterConsistency));
        Assert.False(string.IsNullOrWhiteSpace(FeatureFlagKeys.AutoPlotThreadTracking));
        Assert.False(string.IsNullOrWhiteSpace(FeatureFlagKeys.AutoExtractNovelAssets));
    }
}
