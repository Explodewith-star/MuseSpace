using System.Globalization;
using System.Text;
using MuseSpace.Application.Abstractions.Export;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Contracts.Export;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

namespace MuseSpace.Application.Services.Export;

/// <summary>
/// 章节导出服务实现：内存内拼接，零依赖。
/// 输出 UTF-8 + BOM，兼容 Windows 记事本中文。
/// </summary>
public sealed class ChapterExportService : IChapterExportService
{
    private readonly IStoryProjectRepository _projectRepo;
    private readonly IChapterRepository _chapterRepo;

    public ChapterExportService(
        IStoryProjectRepository projectRepo,
        IChapterRepository chapterRepo)
    {
        _projectRepo = projectRepo;
        _chapterRepo = chapterRepo;
    }

    public async Task<ChapterExportResult?> ExportAsync(
        Guid projectId,
        ChapterExportOptions options,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, cancellationToken);
        if (project is null) return null;

        var allChapters = await _chapterRepo.GetByProjectAsync(projectId, cancellationToken);
        var filtered = allChapters
            .Where(c => options.FromNumber is null || c.Number >= options.FromNumber)
            .Where(c => options.ToNumber is null || c.Number <= options.ToNumber)
            .OrderBy(c => c.Number)
            .ToList();

        var rendered = new List<RenderedChapter>();
        foreach (var c in filtered)
        {
            var (text, isDraft) = SelectBody(c, options);
            if (text is null) continue;
            rendered.Add(new RenderedChapter(c.Number, c.Title, text, isDraft));
        }

        var content = options.Format switch
        {
            ChapterExportFormat.PlainText => RenderPlainText(project.Name, rendered),
            _ => RenderMarkdown(project.Name, rendered),
        };

        // UTF-8 + BOM 兼容 Windows 记事本中文
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var bytes = encoding.GetBytes(content);

        return new ChapterExportResult
        {
            FileName = BuildFileName(project.Name, filtered, options),
            ContentType = options.Format == ChapterExportFormat.PlainText ? "text/plain" : "text/markdown",
            Content = bytes,
            ChapterCount = rendered.Count,
        };
    }

    private static (string? Text, bool IsDraft) SelectBody(Chapter chapter, ChapterExportOptions options)
    {
        var hasFinal = !string.IsNullOrWhiteSpace(chapter.FinalText)
            && chapter.Status == ChapterStatus.Finalized;

        if (hasFinal) return (chapter.FinalText, false);

        if (options.OnlyFinal) return (null, false);

        // OnlyFinal=false 场景：优先使用 FinalText（即便状态未到 Finalized），其次用 DraftText
        if (!string.IsNullOrWhiteSpace(chapter.FinalText)) return (chapter.FinalText, false);

        if (options.IncludeDraftFallback && !string.IsNullOrWhiteSpace(chapter.DraftText))
            return (chapter.DraftText, true);

        return (null, false);
    }

    private static string RenderMarkdown(string projectName, IReadOnlyList<RenderedChapter> chapters)
    {
        var sb = new StringBuilder();
        sb.Append("# 《").Append(projectName).Append("》").AppendLine();
        sb.AppendLine();
        sb.Append("> 导出时间：").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).AppendLine();
        sb.Append("> 共 ").Append(chapters.Count).Append(" 章").AppendLine();
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var c in chapters)
        {
            sb.Append("## 第 ").Append(c.Number).Append(" 章");
            if (!string.IsNullOrWhiteSpace(c.Title))
            {
                sb.Append(" ").Append(c.Title);
            }
            if (c.IsDraft)
            {
                sb.Append("  ").Append("`[草稿]`");
            }
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine(c.Body.Trim());
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string RenderPlainText(string projectName, IReadOnlyList<RenderedChapter> chapters)
    {
        var sb = new StringBuilder();
        sb.Append("《").Append(projectName).Append("》").AppendLine();
        sb.Append("导出时间：").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).AppendLine();
        sb.Append("共 ").Append(chapters.Count).Append(" 章").AppendLine();
        sb.AppendLine();
        sb.AppendLine();

        foreach (var c in chapters)
        {
            sb.Append("第 ").Append(c.Number).Append(" 章");
            if (!string.IsNullOrWhiteSpace(c.Title))
            {
                sb.Append("  ").Append(c.Title);
            }
            if (c.IsDraft)
            {
                sb.Append("  [草稿]");
            }
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine(c.Body.Trim());
            sb.AppendLine();
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildFileName(string projectName, IReadOnlyList<Chapter> rangeChapters, ChapterExportOptions options)
    {
        var sanitized = SanitizeForFileName(projectName);
        var ext = options.Format == ChapterExportFormat.PlainText ? "txt" : "md";

        string rangePart;
        if (rangeChapters.Count == 0)
        {
            rangePart = "全部";
        }
        else
        {
            var minNum = rangeChapters.Min(c => c.Number);
            var maxNum = rangeChapters.Max(c => c.Number);
            rangePart = minNum == maxNum
                ? $"第{minNum}章"
                : $"第{minNum}-{maxNum}章";
        }

        var ts = DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        return $"《{sanitized}》_{rangePart}_{ts}.{ext}";
    }

    private static string SanitizeForFileName(string name)
    {
        // 仅替换 Windows / Linux 都禁用的字符
        var invalid = new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        var sb = new StringBuilder(name);
        foreach (var ch in invalid) sb.Replace(ch, '_');
        return sb.ToString().Trim().TrimEnd('.');
    }

    private sealed record RenderedChapter(int Number, string? Title, string Body, bool IsDraft);
}
