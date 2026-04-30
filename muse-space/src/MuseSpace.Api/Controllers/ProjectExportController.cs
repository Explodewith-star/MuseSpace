using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Abstractions.Export;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.Export;

namespace MuseSpace.Api.Controllers;

/// <summary>
/// 项目导出（章节聚合 → md / txt）。仅同步内存内拼接，不入队。
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/export")]
public sealed class ProjectExportController : ControllerBase
{
    private readonly IChapterExportService _exportService;

    public ProjectExportController(IChapterExportService exportService)
    {
        _exportService = exportService;
    }

    /// <summary>
    /// 导出章节为 md / txt。返回 file stream。
    /// 查询参数：
    ///   format=md|txt，默认 md
    ///   from=起始章号（含），可省
    ///   to=结束章号（含），可省
    ///   onlyFinal=true|false，默认 true
    ///   includeDraft=true|false（仅 onlyFinal=false 时生效），默认 false
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Export(
        Guid projectId,
        [FromQuery] string? format,
        [FromQuery(Name = "from")] int? fromNumber,
        [FromQuery(Name = "to")] int? toNumber,
        [FromQuery] bool onlyFinal = true,
        [FromQuery(Name = "includeDraft")] bool includeDraft = false,
        CancellationToken cancellationToken = default)
    {
        var fmt = ParseFormat(format);
        if (fmt is null)
        {
            return BadRequest(ApiResponse<string>.Fail("不支持的导出格式，仅支持 md / txt"));
        }

        if (fromNumber is not null && toNumber is not null && fromNumber > toNumber)
        {
            return BadRequest(ApiResponse<string>.Fail("起始章号不能大于结束章号"));
        }

        var options = new ChapterExportOptions
        {
            Format = fmt.Value,
            FromNumber = fromNumber,
            ToNumber = toNumber,
            OnlyFinal = onlyFinal,
            IncludeDraftFallback = includeDraft,
        };

        var result = await _exportService.ExportAsync(projectId, options, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<string>.Fail("项目不存在"));
        }

        if (result.ChapterCount == 0)
        {
            return BadRequest(ApiResponse<string>.Fail("该范围内没有可导出的章节"));
        }

        // 用 RFC 5987 编码避免中文文件名乱码
        var encodedFileName = Uri.EscapeDataString(result.FileName);
        Response.Headers["Content-Disposition"] =
            $"attachment; filename=\"{encodedFileName}\"; filename*=UTF-8''{encodedFileName}";

        return File(result.Content, result.ContentType);
    }

    private static ChapterExportFormat? ParseFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return ChapterExportFormat.Markdown;
        return format.Trim().ToLowerInvariant() switch
        {
            "md" or "markdown" => ChapterExportFormat.Markdown,
            "txt" or "text" or "plain" => ChapterExportFormat.PlainText,
            _ => null,
        };
    }
}
