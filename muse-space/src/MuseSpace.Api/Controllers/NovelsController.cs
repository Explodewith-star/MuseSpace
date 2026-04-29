using System.Security.Cryptography;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Abstractions.Storage;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.Novels;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;
using MuseSpace.Infrastructure.Jobs;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/novels")]
public sealed class NovelsController : ControllerBase
{
    private readonly INovelRepository _novelRepo;
    private readonly INovelChunkRepository _chunkRepo;
    private readonly IAgentSuggestionRepository _suggestionRepo;
    private readonly IStorageService _storage;
    private readonly MuseSpaceDbContext _db;

    public NovelsController(
        INovelRepository novelRepo,
        INovelChunkRepository chunkRepo,
        IAgentSuggestionRepository suggestionRepo,
        IStorageService storage,
        MuseSpaceDbContext db)
    {
        _novelRepo = novelRepo;
        _chunkRepo = chunkRepo;
        _suggestionRepo = suggestionRepo;
        _storage = storage;
        _db = db;
    }

    /// <summary>列出项目下所有已导入的小说</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<NovelResponse>>>> List(Guid projectId, CancellationToken ct)
    {
        var novels = await _novelRepo.GetByProjectAsync(projectId, ct);
        return Ok(ApiResponse<List<NovelResponse>>.Ok(novels.Select(ToResponse).ToList()));
    }

    /// <summary>上传并导入小说文件（支持 .txt / .md）</summary>
    [HttpPost]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
    public async Task<IActionResult> Upload(
        Guid projectId,
        IFormFile file,
        [FromQuery] string? title,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not ".txt" and not ".md")
            return BadRequest(new { message = "Only .txt and .md files are supported." });

        // Read bytes and compute SHA-256 for deduplication
        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            var buffer = ms.GetBuffer().AsSpan(0, checked((int)ms.Length));
            var hash = Convert.ToHexString(SHA256.HashData(buffer)).ToLowerInvariant();

            var existing = await _novelRepo.GetByProjectAndHashAsync(projectId, hash, ct);
            if (existing is not null)
                return Conflict(ApiResponse<NovelResponse>.Fail("This file has already been imported for this project."));

            var novelId = Guid.NewGuid();
            var fileKey = $"raw/{novelId}{ext}";

            ms.Position = 0;
            await _storage.SaveAsync(fileKey, ms, ct);

            // Create novel record
            var novel = new Novel
            {
                Id = novelId,
                StoryProjectId = projectId,
                Title = title ?? Path.GetFileNameWithoutExtension(file.FileName),
                FileName = file.FileName,
                FileKey = fileKey,
                FileHash = hash,
                FileSize = file.Length,
                Status = NovelStatus.Pending,
                ProgressDone = 0,
                ProgressTotal = 0,
                LastError = null,
                StartedAt = null,
                FinishedAt = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _novelRepo.AddAsync(novel, ct);

            // Enqueue Hangfire job chain: Chunking → Embedding
            var jobId = BackgroundJob.Enqueue<ChunkNovelJob>(j => j.ExecuteAsync(novelId, null));
            BackgroundJob.ContinueJobWith<EmbedNovelJob>(jobId, j => j.ExecuteAsync(novelId, null));

            return Ok(ApiResponse<NovelResponse>.Ok(ToResponse(novel)));
        }
    }

    /// <summary>查询单个小说的导入状态（轮询 fallback）</summary>
    [HttpGet("{novelId:guid}/status")]
    public async Task<ActionResult<ApiResponse<NovelResponse>>> GetStatus(Guid projectId, Guid novelId, CancellationToken ct)
    {
        var novel = await _novelRepo.GetByIdAsync(novelId, ct);
        if (novel is null || novel.StoryProjectId != projectId)
            return NotFound(ApiResponse<NovelResponse>.Fail("Novel not found"));

        return Ok(ApiResponse<NovelResponse>.Ok(ToResponse(novel)));
    }

    /// <summary>删除小说及其切片、向量数据，以及本书产出的未应用建议</summary>
    [HttpDelete("{novelId:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid projectId, Guid novelId, CancellationToken ct)
    {
        var novel = await _novelRepo.GetByIdAsync(novelId, ct);
        if (novel is null || novel.StoryProjectId != projectId)
            return NotFound(ApiResponse<bool>.Fail("Novel not found"));

        if (novel.FileKey is not null)
            await _storage.DeleteAsync(novel.FileKey, ct);

        // 1. 清理本书产出的未应用建议（Applied 的已转为正式资产，保留）
        await _suggestionRepo.DeleteBySourceNovelIdAsync(novelId, ct);

        // 2. 清 embeddings（跨 schema，raw SQL），再清 chunks，最后删主记录
        await _db.Database.ExecuteSqlRawAsync(
            "DELETE FROM memory.chunk_embeddings WHERE \"ChunkId\" IN (SELECT \"Id\" FROM novel_chunks WHERE \"NovelId\" = {0})", novelId);
        await _chunkRepo.DeleteByNovelAsync(novelId, ct);
        await _novelRepo.DeleteAsync(novelId, ct);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    private static NovelResponse ToResponse(Novel n) => new()
    {
        Id = n.Id,
        StoryProjectId = n.StoryProjectId,
        Title = n.Title,
        FileName = n.FileName,
        FileSize = n.FileSize,
        Status = n.Status.ToString(),
        TotalChunks = n.TotalChunks,
        ProgressDone = n.ProgressDone,
        ProgressTotal = n.ProgressTotal,
        LastError = n.LastError,
        StartedAt = n.StartedAt,
        FinishedAt = n.FinishedAt,
        CreatedAt = n.CreatedAt
    };
}
