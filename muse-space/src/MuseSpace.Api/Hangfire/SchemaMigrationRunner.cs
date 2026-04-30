using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Api.Hangfire;

/// <summary>
/// 轻量级迁移版本记录器：用 schema_migrations 表保证每个 version 在数据库上只跑一次。
/// 替代过去"每次启动重跑幂等 SQL"的做法 —— 即便 SQL 是幂等的，重复 UPDATE 也会
/// 把用户事后修改的数据再分类回去（参见 LegacyConsistencyCategoryMigration 的 title 前缀匹配）。
/// </summary>
public sealed class SchemaMigrationRunner
{
    private readonly MuseSpaceDbContext _db;
    private readonly ILogger _logger;

    public SchemaMigrationRunner(MuseSpaceDbContext db, ILogger logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 确保 schema_migrations 表存在。
    /// </summary>
    public async Task EnsureTableAsync(CancellationToken ct)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                "Version" varchar(100) PRIMARY KEY,
                "AppliedAt" timestamptz NOT NULL DEFAULT now()
            );
            """;
        await _db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    /// <summary>
    /// 如果 version 未跑过，则执行 action 并登记；事务保证两者原子。
    /// </summary>
    public async Task<bool> RunOnceAsync(string version, Func<MuseSpaceDbContext, CancellationToken, Task> action, CancellationToken ct)
    {
        await EnsureTableAsync(ct);

        // 幂等检查（避免依赖 EF SqlQueryRaw 的列名约定，直接走 ADO.NET）
        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT 1 FROM schema_migrations WHERE \"Version\" = @v";
            var p = cmd.CreateParameter();
            p.ParameterName = "@v";
            p.Value = version;
            cmd.Parameters.Add(p);
            var exists = await cmd.ExecuteScalarAsync(ct);
            if (exists is not null)
            {
                _logger.LogDebug("[Migration] {Version} already applied, skipped", version);
                return false;
            }
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await action(_db, ct);
            await _db.Database.ExecuteSqlRawAsync(
                "INSERT INTO schema_migrations(\"Version\") VALUES ({0}) ON CONFLICT DO NOTHING",
                [version], ct);
            await tx.CommitAsync(ct);
            _logger.LogInformation("[Migration] {Version} applied", version);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
