using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Agents;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Services.Agents;
using MuseSpace.Application.Services.Suggestions;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Hangfire Job：项目摘要生成。
/// 汇总项目当前角色 / 世界观 / 大纲 / 草稿覆盖度，调用 ProjectSummary Agent，
/// 结果写入一条 ProjectSummary 类目建议进入建议中心。
/// </summary>
public sealed class ProjectSummaryJob
{
    private const string TaskType = "project-summary";

    private readonly IAgentRunner _agentRunner;
    private readonly ICharacterRepository _characterRepo;
    private readonly IWorldRuleRepository _worldRuleRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly AgentSuggestionAppService _suggestionService;
    private readonly IAgentProgressNotifier _progressNotifier;
    private readonly LlmProviderSelector _selector;
    private readonly MuseSpaceDbContext _db;
    private readonly ILogger<ProjectSummaryJob> _logger;

    public ProjectSummaryJob(
        IAgentRunner agentRunner,
        ICharacterRepository characterRepo,
        IWorldRuleRepository worldRuleRepo,
        IChapterRepository chapterRepo,
        AgentSuggestionAppService suggestionService,
        IAgentProgressNotifier progressNotifier,
        LlmProviderSelector selector,
        MuseSpaceDbContext db,
        ILogger<ProjectSummaryJob> logger)
    {
        _agentRunner = agentRunner;
        _characterRepo = characterRepo;
        _worldRuleRepo = worldRuleRepo;
        _chapterRepo = chapterRepo;
        _suggestionService = suggestionService;
        _progressNotifier = progressNotifier;
        _selector = selector;
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid projectId, string? userInput, Guid? userId)
    {
        _logger.LogInformation("[ProjectSummary] Start project={ProjectId}", projectId);
        await ApplyUserLlmPreferenceAsync(userId);
        await _progressNotifier.NotifyStartedAsync(projectId, TaskType);

        try
        {
            // 1. 收集快照
            var characters = await _characterRepo.GetByProjectAsync(projectId);
            var rules = await _worldRuleRepo.GetByProjectAsync(projectId);
            var chapters = (await _chapterRepo.GetByProjectAsync(projectId)).OrderBy(c => c.Number).ToList();

            var totalChapters = chapters.Count;
            var draftedChapters = chapters.Count(c => !string.IsNullOrWhiteSpace(c.DraftText));
            var plannedChapters = chapters.Count(c =>
                !string.IsNullOrWhiteSpace(c.Goal) || !string.IsNullOrWhiteSpace(c.Summary));

            var snapshot = $$"""
                ## 项目快照

                角色总数：{{characters.Count}}
                世界观规则：{{rules.Count}}（硬约束 {{rules.Count(r => r.IsHardConstraint)}} 条）
                章节总数：{{totalChapters}}（已填章节计划 {{plannedChapters}}，已生成草稿 {{draftedChapters}}）

                ## 已完成章节标题

                {{(chapters.Take(20).Where(c => !string.IsNullOrWhiteSpace(c.DraftText))
                    .Select(c => $"- 第{c.Number}章 {c.Title}").DefaultIfEmpty("（暂无）")
                    .Aggregate((a, b) => a + "\n" + b))}}

                ## 待写章节大纲（前 10 章）

                {{(chapters.Where(c => string.IsNullOrWhiteSpace(c.DraftText)).Take(10)
                    .Select(c => $"- 第{c.Number}章 {c.Title}：{c.Goal ?? "（无目标）"}")
                    .DefaultIfEmpty("（暂无）")
                    .Aggregate((a, b) => a + "\n" + b))}}

                {{(string.IsNullOrWhiteSpace(userInput) ? "" : "## 作者补充关注点\n\n" + userInput)}}
                """;

            await _progressNotifier.NotifyGeneratingAsync(projectId, TaskType);

            var ctx = new AgentRunContext { UserId = userId, ProjectId = projectId };
            var result = await _agentRunner.RunAsync(
                ProjectSummaryAgentDefinition.AgentName, snapshot, ctx);

            if (!result.Success)
            {
                await _progressNotifier.NotifyFailedAsync(projectId, TaskType,
                    result.ErrorMessage ?? "Agent 执行失败");
                return;
            }

            // 2. 解析 JSON
            var json = result.Output.Trim();
            if (json.StartsWith("```")) json = Regex.Replace(json, @"```\w*\n?", "").Trim('`').Trim();

            // 不强制解析具体字段，直接以原始 JSON 入库展示。
            // 为防止非法 JSON，做一次校验：失败则降级为纯文本对象。
            string contentJson;
            try
            {
                using var doc = JsonDocument.Parse(json);
                contentJson = doc.RootElement.GetRawText();
            }
            catch
            {
                contentJson = JsonSerializer.Serialize(new { headline = result.Output });
            }

            await _suggestionService.CreateAsync(
                agentRunId: ctx.RunId,
                storyProjectId: projectId,
                category: SuggestionCategories.ProjectSummary,
                title: $"项目摘要 · {DateTime.Now:yyyy-MM-dd HH:mm}",
                contentJson: contentJson);

            await _progressNotifier.NotifyDoneAsync(projectId, TaskType, "项目摘要已生成，请查看建议中心");
            _logger.LogInformation("[ProjectSummary] Done project={ProjectId}", projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProjectSummary] Unexpected error project={ProjectId}", projectId);
            await _progressNotifier.NotifyFailedAsync(projectId, TaskType, "项目摘要生成失败");
        }
    }

    private async Task ApplyUserLlmPreferenceAsync(Guid? userId)
    {
        if (userId is null) return;
        var pref = await _db.UserLlmPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId.Value);
        if (pref is null) return;
        if (Enum.TryParse<MuseSpace.Application.Abstractions.Llm.LlmProviderType>(pref.Provider, ignoreCase: true, out var provider))
            _selector.Active = provider;
        if (!string.IsNullOrWhiteSpace(pref.ModelId))
            _selector.ActiveModel = pref.ModelId;
    }
}
