using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Agents;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Services.Agents;
using MuseSpace.Application.Services.Suggestions;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Hangfire Job：文风一致性审查。
/// 由章节草稿生成完成后异步触发；项目无 StyleProfile 时跳过。
/// 每条偏离写入一条 Consistency 类型 AgentSuggestion，TargetEntityId 指向章节。
/// </summary>
public sealed class StyleConsistencyCheckJob
{
    private readonly IAgentRunner _agentRunner;
    private readonly IStyleProfileRepository _styleRepo;
    private readonly AgentSuggestionAppService _suggestionService;
    private readonly LlmProviderSelector _selector;
    private readonly MuseSpaceDbContext _db;
    private readonly ILogger<StyleConsistencyCheckJob> _logger;

    public StyleConsistencyCheckJob(
        IAgentRunner agentRunner,
        IStyleProfileRepository styleRepo,
        AgentSuggestionAppService suggestionService,
        LlmProviderSelector selector,
        MuseSpaceDbContext db,
        ILogger<StyleConsistencyCheckJob> logger)
    {
        _agentRunner = agentRunner;
        _styleRepo = styleRepo;
        _suggestionService = suggestionService;
        _selector = selector;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 执行文风一致性检查。
    /// </summary>
    /// <param name="projectId">项目 Id。</param>
    /// <param name="chapterId">触发的章节 Id（用于 TargetEntityId）。可为 Guid.Empty。</param>
    /// <param name="draftText">待审查的草稿文本。</param>
    /// <param name="userId">用户 Id（可选）。</param>
    public async Task ExecuteAsync(Guid projectId, Guid chapterId, string draftText, Guid? userId)
    {
        if (string.IsNullOrWhiteSpace(draftText))
        {
            _logger.LogInformation("[StyleConsistency] Empty draft, skip");
            return;
        }

        await ApplyUserLlmPreferenceAsync(userId);

        var profile = await _styleRepo.GetByProjectAsync(projectId);
        if (profile is null)
        {
            _logger.LogInformation("[StyleConsistency] No style profile for project {ProjectId}, skip", projectId);
            return;
        }

        var profileLines = new List<string> { $"[文风画像] {profile.Name}" };
        if (!string.IsNullOrWhiteSpace(profile.Tone)) profileLines.Add($"  语气: {profile.Tone}");
        if (!string.IsNullOrWhiteSpace(profile.SentenceLengthPreference)) profileLines.Add($"  句式偏好: {profile.SentenceLengthPreference}");
        if (!string.IsNullOrWhiteSpace(profile.DialogueRatio)) profileLines.Add($"  对话比重: {profile.DialogueRatio}");
        if (!string.IsNullOrWhiteSpace(profile.DescriptionDensity)) profileLines.Add($"  描写密度: {profile.DescriptionDensity}");
        if (!string.IsNullOrWhiteSpace(profile.ForbiddenExpressions)) profileLines.Add($"  禁用表达: {profile.ForbiddenExpressions}");
        if (!string.IsNullOrWhiteSpace(profile.SampleReferenceText)) profileLines.Add($"  示例段落: {Truncate(profile.SampleReferenceText, 400)}");

        var profileText = string.Join("\n", profileLines);

        var prompt = $$"""
            ## 项目文风画像

            {{profileText}}

            ## 待检查草稿

            {{draftText}}

            请按照系统提示中的维度审查，以纯 JSON 数组格式返回偏离点。
            """;

        var ctx = new AgentRunContext { UserId = userId, ProjectId = projectId };
        var result = await _agentRunner.RunAsync(
            StyleConsistencyAgentDefinition.AgentName, prompt, ctx);

        if (!result.Success)
        {
            _logger.LogWarning("[StyleConsistency] Agent failed: {Err}", result.ErrorMessage);
            return;
        }

        var json = result.Output.Trim();
        if (json.StartsWith("```"))
            json = Regex.Replace(json, @"```\w*\n?", "").Trim('`').Trim();

        List<StyleDeviationItem> items;
        try
        {
            items = JsonSerializer.Deserialize<List<StyleDeviationItem>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[StyleConsistency] Parse failed");
            return;
        }

        if (items.Count == 0)
        {
            _logger.LogInformation("[StyleConsistency] No deviations for project {ProjectId}", projectId);
            return;
        }

        foreach (var item in items)
        {
            var contentJson = JsonSerializer.Serialize(new
            {
                ChapterId = chapterId == Guid.Empty ? (Guid?)null : chapterId,
                item.Dimension,
                item.Severity,
                item.Excerpt,
                item.Issue,
                item.Suggestion,
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
            });

            var dim = string.IsNullOrWhiteSpace(item.Dimension) ? "文风" : item.Dimension;
            await _suggestionService.CreateAsync(
                agentRunId: ctx.RunId,
                storyProjectId: projectId,
                category: SuggestionCategories.Consistency,
                title: $"文风偏离：{dim}",
                contentJson: contentJson,
                targetEntityId: chapterId == Guid.Empty ? null : chapterId);
        }

        _logger.LogInformation("[StyleConsistency] Saved {Count} deviations for project {ProjectId}",
            items.Count, projectId);
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "...";

    private async Task ApplyUserLlmPreferenceAsync(Guid? userId)
    {
        if (userId is null) return;
        var pref = await _db.UserLlmPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId.Value);
        if (pref is null) return;
        if (Enum.TryParse<LlmProviderType>(pref.Provider, ignoreCase: true, out var provider))
            _selector.Active = provider;
        if (!string.IsNullOrWhiteSpace(pref.ModelId))
            _selector.ActiveModel = pref.ModelId;
    }

    private sealed class StyleDeviationItem
    {
        public string? Dimension { get; set; }
        public string? Severity { get; set; }
        public string? Excerpt { get; set; }
        public string? Issue { get; set; }
        public string? Suggestion { get; set; }
    }
}
