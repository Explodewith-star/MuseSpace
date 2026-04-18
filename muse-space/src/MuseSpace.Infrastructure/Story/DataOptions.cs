namespace MuseSpace.Infrastructure.Story;

/// <summary>
/// data/ 目录的配置选项，由 appsettings.json 中的 "Data" 节点绑定。
/// </summary>
public sealed class DataOptions
{
    public const string SectionName = "Data";

    /// <summary>data 目录的绝对路径，由启动时解析注入。</summary>
    public string BasePath { get; set; } = string.Empty;
}
