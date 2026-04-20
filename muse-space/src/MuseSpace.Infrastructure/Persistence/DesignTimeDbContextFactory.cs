using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MuseSpace.Infrastructure.Persistence;

/// <summary>
/// 仅供 `dotnet ef migrations add` 使用的设计时工厂。
/// 运行 migrations 前请先设置环境变量 MUSESPACE_CONN，例如：
///   $env:MUSESPACE_CONN="Host=152.136.11.140;Port=6286;Database=musespace_dev;Username=msadmin;Password=YOUR_PWD"
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MuseSpaceDbContext>
{
    public MuseSpaceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MUSESPACE_CONN")
            ?? throw new InvalidOperationException(
                "请先设置环境变量 MUSESPACE_CONN，值为 PostgreSQL 连接字符串，" +
                "例如：Host=152.136.11.140;Port=6286;Database=musespace_dev;Username=msadmin;Password=YOUR_PWD");

        var optionsBuilder = new DbContextOptionsBuilder<MuseSpaceDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o => o.UseVector());

        return new MuseSpaceDbContext(optionsBuilder.Options);
    }
}
