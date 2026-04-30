using Hangfire;
using Microsoft.AspNetCore.Mvc;
using MuseSpace.Api.Authorization;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Contracts.Agents;
using MuseSpace.Contracts.Common;
using MuseSpace.Infrastructure.Jobs;
using System.Security.Claims;

namespace MuseSpace.Api.Controllers;

/// <summary>
/// D3-2 菜单 Agent 化统一触发入口。
/// 任何菜单（角色 / 世界观 / 文风 / 概览 / 章节）都通过本接口触发后端 Agent，
/// 后端按 <see cref="AgentTaskRequest.AgentType"/> 路由到对应 Hangfire Job。
/// 结果统一进入 AgentSuggestion 建议表，前端通过建议中心审核。
/// </summary>
[ApiController]
[ProjectAccess]
[Route("api/projects/{projectId:guid}/agent-tasks")]
public class AgentTasksController : ControllerBase
{
    private const int MaxAllDraftsLength = 60_000; // 防超长截断阈值

    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IChapterRepository _chapterRepo;
    private readonly IActiveAgentTaskRegistry _activeRegistry;

    public AgentTasksController(
        IBackgroundJobClient backgroundJobs,
        IChapterRepository chapterRepo,
        IActiveAgentTaskRegistry activeRegistry)
    {
        _backgroundJobs = backgroundJobs;
        _chapterRepo = chapterRepo;
        _activeRegistry = activeRegistry;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>查询当前项目下所有活跃 Agent 任务（用于 SignalR 重连后恢复进度展示）。</summary>
    [HttpGet("active")]
    public ActionResult<ApiResponse<IReadOnlyList<ActiveAgentTaskInfo>>> GetActive(Guid projectId)
        => Ok(ApiResponse<IReadOnlyList<ActiveAgentTaskInfo>>.Ok(_activeRegistry.GetByProject(projectId)));

    /// <summary>触发一个 Agent 任务。</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<AgentTaskResponse>>> Trigger(
        Guid projectId, [FromBody] AgentTaskRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AgentType))
            return BadRequest(ApiResponse<AgentTaskResponse>.Fail("agentType 不能为空"));

        var agentType = request.AgentType.Trim();
        var userId = CurrentUserId;

        switch (agentType)
        {
            // ── 资产提取类 ─────────────────────────────────────────
            case "character-extract":
            case "worldrule-extract":
            case "styleprofile-extract":
            case "extract-all":
                {
                    var jobId = _backgroundJobs.Enqueue<ExtractNovelAssetsJob>(j =>
                        j.ExecuteSingleAsync(projectId, agentType, request.NovelId, request.UserInput, userId));
                    return Ok(ApiResponse<AgentTaskResponse>.Ok(new AgentTaskResponse
                    {
                        TaskId = jobId,
                        TaskType = "asset-extract",
                        Message = "任务已提交，完成后请在建议中心查看",
                    }));
                }

            // ── 世界观 / 角色一致性审查（基于文本） ──────────────────
            case "consistency-check":
            case "character-consistency":
                {
                    var (text, error) = await ResolveCheckTextAsync(projectId, request, cancellationToken);
                    if (text is null)
                        return BadRequest(ApiResponse<AgentTaskResponse>.Fail(error ?? "无法解析待审查文本"));

                    if (agentType == "consistency-check")
                    {
                        var jobId = _backgroundJobs.Enqueue<ConsistencyCheckJob>(j =>
                            j.ExecuteAsync(projectId, text, userId, "草稿"));
                        return Ok(ApiResponse<AgentTaskResponse>.Ok(new AgentTaskResponse
                        {
                            TaskId = jobId,
                            TaskType = "consistency",
                            Message = "世界观一致性审查已提交",
                        }));
                    }
                    else
                    {
                        var jobId = _backgroundJobs.Enqueue<CharacterConsistencyCheckJob>(j =>
                            j.ExecuteAsync(projectId, text, userId));
                        return Ok(ApiResponse<AgentTaskResponse>.Ok(new AgentTaskResponse
                        {
                            TaskId = jobId,
                            TaskType = "character-consistency",
                            Message = "角色一致性审查已提交",
                        }));
                    }
                }

            // ── 文风一致性审查（必须指定章节） ────────────────────
            case "style-consistency":
                {
                    if (request.ChapterId is null)
                        return BadRequest(ApiResponse<AgentTaskResponse>.Fail("文风审查必须指定 chapterId"));

                    var chapter = await _chapterRepo.GetByIdAsync(projectId, request.ChapterId.Value, cancellationToken);
                    if (chapter is null)
                        return NotFound(ApiResponse<AgentTaskResponse>.Fail("章节不存在"));
                    var draft = chapter.DraftText;
                    if (string.IsNullOrWhiteSpace(draft))
                        return BadRequest(ApiResponse<AgentTaskResponse>.Fail("该章节尚无草稿，无法审查"));

                    var jobId = _backgroundJobs.Enqueue<StyleConsistencyCheckJob>(j =>
                        j.ExecuteAsync(projectId, request.ChapterId.Value, draft, userId));
                    return Ok(ApiResponse<AgentTaskResponse>.Ok(new AgentTaskResponse
                    {
                        TaskId = jobId,
                        TaskType = "style-consistency",
                        Message = "文风审查已提交",
                    }));
                }

            // ── 章节自动规划 ──────────────────────────────────────
            case "chapter-auto-plan":
                {
                    if (request.ChapterId is null)
                        return BadRequest(ApiResponse<AgentTaskResponse>.Fail("章节自动规划必须指定 chapterId"));

                    var jobId = _backgroundJobs.Enqueue<ChapterAutoPlanJob>(j =>
                        j.ExecuteAsync(projectId, request.ChapterId.Value, userId));
                    return Ok(ApiResponse<AgentTaskResponse>.Ok(new AgentTaskResponse
                    {
                        TaskId = jobId,
                        TaskType = "chapter-auto-plan",
                        Message = "章节自动规划已提交",
                    }));
                }

            // ── 项目摘要 ─────────────────────────────────────────
            case "project-summary":
                {
                    var jobId = _backgroundJobs.Enqueue<ProjectSummaryJob>(j =>
                        j.ExecuteAsync(projectId, request.UserInput, userId));
                    return Ok(ApiResponse<AgentTaskResponse>.Ok(new AgentTaskResponse
                    {
                        TaskId = jobId,
                        TaskType = "project-summary",
                        Message = "项目摘要已提交，完成后请在建议中心查看",
                    }));
                }

            // ── 伏笔追踪扫描 ─────────────────────────────────────
            case "plot-thread-scan":
                {
                    var jobId = _backgroundJobs.Enqueue<PlotThreadTrackingJob>(j =>
                        j.ExecuteAsync(projectId, request.ChapterId, userId));
                    return Ok(ApiResponse<AgentTaskResponse>.Ok(new AgentTaskResponse
                    {
                        TaskId = jobId,
                        TaskType = "plot-thread-tracking",
                        Message = "伏笔扫描已提交",
                    }));
                }

            default:
                return BadRequest(ApiResponse<AgentTaskResponse>.Fail($"暂不支持的 agentType: {agentType}"));
        }
    }

    /// <summary>
    /// 根据请求中的 Scope 解析待审查文本。
    /// </summary>
    /// <returns>(文本, 错误消息)，若文本为 null 则视为失败。</returns>
    private async Task<(string? Text, string? Error)> ResolveCheckTextAsync(
        Guid projectId, AgentTaskRequest req, CancellationToken ct)
    {
        var scope = string.IsNullOrWhiteSpace(req.Scope) ? "latest-draft" : req.Scope!.Trim();
        switch (scope)
        {
            case "raw-text":
                if (string.IsNullOrWhiteSpace(req.RawText))
                    return (null, "scope=raw-text 时 rawText 不能为空");
                return (req.RawText, null);

            case "all-drafts":
                {
                    var all = await _chapterRepo.GetByProjectAsync(projectId, ct);
                    var ordered = all.OrderBy(c => c.Number).ToList();
                    var sb = new System.Text.StringBuilder();
                    foreach (var ch in ordered)
                    {
                        if (string.IsNullOrWhiteSpace(ch.DraftText)) continue;
                        sb.Append("【第").Append(ch.Number).Append("章 ").Append(ch.Title ?? "无标题").Append("】\n");
                        sb.Append(ch.DraftText).Append("\n\n");
                        if (sb.Length >= MaxAllDraftsLength) break;
                    }
                    if (sb.Length == 0) return (null, "项目下没有任何已生成的草稿");
                    return (sb.ToString(), null);
                }

            case "latest-draft":
            default:
                {
                    if (req.ChapterId is null)
                        return (null, "scope=latest-draft 时必须指定 chapterId");
                    var chapter = await _chapterRepo.GetByIdAsync(projectId, req.ChapterId.Value, ct);
                    if (chapter is null) return (null, "章节不存在");
                    if (string.IsNullOrWhiteSpace(chapter.DraftText)) return (null, "该章节尚无草稿");
                    return (chapter.DraftText, null);
                }
        }
    }
}

