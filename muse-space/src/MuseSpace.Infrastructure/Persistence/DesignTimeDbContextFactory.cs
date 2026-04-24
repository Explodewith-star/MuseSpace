using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MuseSpace.Infrastructure.Persistence;

/// <summary>
/// 仅供 <c>dotnet ef migrations add / database update</c> 等设计时命令使用。
/// 连接字符串解析优先级：
///   1. 环境变量 MUSESPACE_CONN（CI/CD 或特殊场景覆盖用）
///   2. appsettings.Development.json → ConnectionStrings:DefaultConnection
///   3. appsettings.json → ConnectionStrings:DefaultConnection
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MuseSpaceDbContext>
{
    public MuseSpaceDbContext CreateDbContext(string[] args)
    {
        // 优先环境变量（兼容 CI 和旧习惯）
        var connectionString = Environment.GetEnvironmentVariable("MUSESPACE_CONN");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // 回退到 appsettings 配置文件
            var configuration = new ConfigurationBuilder()
                .SetBasePath(FindApiProjectDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "未找到数据库连接字符串。请确保 appsettings.Development.json 中配置了 ConnectionStrings:DefaultConnection，" +
                "或设置环境变量 MUSESPACE_CONN。");

        var optionsBuilder = new DbContextOptionsBuilder<MuseSpaceDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o => o.UseVector());

        return new MuseSpaceDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// 定位 MuseSpace.Api 项目目录（appsettings.json 所在位置）。
    /// 从当前文件所在的 Infrastructure 项目向上找 src/MuseSpace.Api。
    /// </summary>
    private static string FindApiProjectDirectory()
    {
        // 设计时工作目录通常是解决方案根目录或 --startup-project 指定的目录
        // 尝试多个常见路径
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "src", "MuseSpace.Api"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "MuseSpace.Api"),
            Directory.GetCurrentDirectory(), // 如果 --startup-project 已经指向 Api
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(full, "appsettings.json"))
                || File.Exists(Path.Combine(full, "appsettings.Development.json")))
            {
                return full;
            }
        }

        // 兜底：直接用当前目录
        return Directory.GetCurrentDirectory();
    }
}
