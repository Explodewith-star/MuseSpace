using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MuseSpace.Contracts.Common;
using MuseSpace.Domain.Entities;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Api.Controllers;

/// <summary>
/// D4-D2 管理员 AgentRun 面板：列出运行记录、查看详情、统计成功率/平均耗时/平均 token。
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/agent-runs")]
public class AdminAgentRunsController : ControllerBase
{
    private readonly MuseSpaceDbContext _db;
    public AdminAgentRunsController(MuseSpaceDbContext db) => _db = db;

    public sealed class AgentRunListItem
    {
        public Guid Id { get; set; }
        public string AgentName { get; set; } = "";
        public Guid? UserId { get; set; }
        public Guid? ProjectId { get; set; }
        public string Status { get; set; } = "";
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public long DurationMs { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public sealed class AgentRunListResponse
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<AgentRunListItem> Items { get; set; } = [];
    }

    public sealed class AgentRunStatsResponse
    {
        public int TotalRuns { get; set; }
        public int SucceededRuns { get; set; }
        public int FailedRuns { get; set; }
        public double SuccessRate { get; set; }
        public double AvgDurationMs { get; set; }
        public double AvgTotalTokens { get; set; }
        public List<AgentNameStat> ByAgent { get; set; } = [];
    }

    public sealed class AgentNameStat
    {
        public string AgentName { get; set; } = "";
        public int Total { get; set; }
        public int Succeeded { get; set; }
        public double AvgDurationMs { get; set; }
    }

    /// <summary>
    /// 列出运行记录。支持 agentName / status 过滤，分页。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<AgentRunListResponse>>> GetList(
        [FromQuery] string? agentName,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 50;

        var q = _db.AgentRuns.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(agentName)) q = q.Where(r => r.AgentName == agentName);
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<AgentRunStatus>(status, true, out var s)) q = q.Where(r => r.Status == s);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.StartedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new AgentRunListItem
            {
                Id = r.Id,
                AgentName = r.AgentName,
                UserId = r.UserId,
                ProjectId = r.ProjectId,
                Status = r.Status.ToString(),
                InputTokens = r.InputTokens,
                OutputTokens = r.OutputTokens,
                DurationMs = r.DurationMs,
                StartedAt = r.StartedAt,
                FinishedAt = r.FinishedAt,
                ErrorMessage = r.ErrorMessage,
            }).ToListAsync(ct);

        return Ok(ApiResponse<AgentRunListResponse>.Ok(new AgentRunListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = items,
        }));
    }

    /// <summary>查看单条运行的完整 input/output preview。</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<AgentRun>>> GetById(Guid id, CancellationToken ct)
    {
        var run = await _db.AgentRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return run is null
            ? NotFound(ApiResponse<AgentRun>.Fail("运行记录不存在"))
            : Ok(ApiResponse<AgentRun>.Ok(run));
    }

    /// <summary>近 N 天的全局统计。</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<AgentRunStatsResponse>>> GetStats(
        [FromQuery] int days = 7, CancellationToken ct = default)
    {
        if (days is < 1 or > 90) days = 7;
        var since = DateTime.UtcNow.AddDays(-days);
        var rows = await _db.AgentRuns.AsNoTracking()
            .Where(r => r.StartedAt >= since)
            .Select(r => new { r.AgentName, r.Status, r.DurationMs, r.InputTokens, r.OutputTokens })
            .ToListAsync(ct);

        var total = rows.Count;
        var succeeded = rows.Count(r => r.Status == AgentRunStatus.Succeeded);
        var failed = rows.Count(r => r.Status == AgentRunStatus.Failed);
        var byAgent = rows.GroupBy(r => r.AgentName)
            .Select(g => new AgentNameStat
            {
                AgentName = g.Key,
                Total = g.Count(),
                Succeeded = g.Count(x => x.Status == AgentRunStatus.Succeeded),
                AvgDurationMs = g.Count() > 0 ? g.Average(x => x.DurationMs) : 0,
            }).OrderByDescending(s => s.Total).ToList();

        return Ok(ApiResponse<AgentRunStatsResponse>.Ok(new AgentRunStatsResponse
        {
            TotalRuns = total,
            SucceededRuns = succeeded,
            FailedRuns = failed,
            SuccessRate = total == 0 ? 0 : Math.Round((double)succeeded / total, 4),
            AvgDurationMs = total == 0 ? 0 : Math.Round(rows.Average(r => r.DurationMs), 1),
            AvgTotalTokens = total == 0 ? 0 : Math.Round(rows.Average(r => r.InputTokens + r.OutputTokens), 1),
            ByAgent = byAgent,
        }));
    }
}
