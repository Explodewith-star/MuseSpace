using MuseSpace.Contracts.Export;

namespace MuseSpace.Application.Abstractions.Export;

/// <summary>
/// 章节导出服务：把项目章节聚合为单个 md / txt 文件。
/// 仅做内存内字符串拼接，不涉及对象存储或异步队列。
/// </summary>
public interface IChapterExportService
{
    Task<ChapterExportResult?> ExportAsync(
        Guid projectId,
        ChapterExportOptions options,
        CancellationToken cancellationToken = default);
}
