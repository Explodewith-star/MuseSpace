using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Agents;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Services.Agents;
using MuseSpace.Application.Services.Suggestions;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Hangfire Job：在原著导入完成（Indexed）后自动提取候选资产。
/// 串行调用角色提取、世界观提取、文风提取三个 Agent，
/// 结果写入 AgentSuggestion 建议表，等待用户审核。
/// </summary>
public sealed class ExtractNovelAssetsJob
{
    private const int SampleChunkCount = 20;

    private readonly IAgentRunner _agentRunner;
    private readonly INovelRepository _novelRepo;
    private readonly INovelChunkRepository _chunkRepo;
    private readonly AgentSuggestionAppService _suggestionService;
    private readonly IAgentProgressNotifier _progressNotifier;
    private readonly LlmProviderSelector _selector;
    private readonly MuseSpaceDbContext _db;
    private readonly ILogger<ExtractNovelAssetsJob> _logger;

    public ExtractNovelAssetsJob(
        IAgentRunner agentRunner,
        INovelRepository novelRepo,
        INovelChunkRepository chunkRepo,
        AgentSuggestionAppService suggestionService,
        IAgentProgressNotifier progressNotifier,
        LlmProviderSelector selector,
        MuseSpaceDbContext db,
        ILogger<ExtractNovelAssetsJob> logger)
    {
        _agentRunner = agentRunner;
        _novelRepo = novelRepo;
        _chunkRepo = chunkRepo;
        _suggestionService = suggestionService;
        _progressNotifier = progressNotifier;
        _selector = selector;
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid novelId, Guid? userId)
    {
        const string taskType = "asset-extract";

        _logger.LogInformation("[AssetExtract] Start for novel {NovelId}", novelId);

        var novel = await _novelRepo.GetByIdAsync(novelId);
        if (novel is null)
        {
            _logger.LogWarning("[AssetExtract] Novel {NovelId} not found, skip", novelId);
            return;
        }

        // 加载用户 LLM 偏好：优先用传入的 userId，否则从所属项目中获取
        var effectiveUserId = userId;
        if (effectiveUserId is null)
        {
            effectiveUserId = await _db.StoryProjects
                .AsNoTracking()
                .Where(p => p.Id == novel.StoryProjectId)
                .Select(p => p.UserId)
                .FirstOrDefaultAsync();
        }
        await ApplyUserLlmPreferenceAsync(effectiveUserId);

        await _progressNotifier.NotifyStartedAsync(novel.StoryProjectId, taskType);

        try
        {
            // 1. 采样切片（均匀覆盖首、中、尾）
            var allChunks = await _chunkRepo.GetByNovelAsync(novelId);
            var sampled = SampleChunks(allChunks.Select(c => c.Content).ToList(), SampleChunkCount);
            var sampledText = string.Join("\n\n---\n\n", sampled);

            _logger.LogInformation("[AssetExtract] Sampled {Count} chunks, total chars {Length}",
                sampled.Count, sampledText.Length);

            // 2. 角色提取（每次独立 RunId，避免 EF Core tracking 冲突）
            await _progressNotifier.NotifyGeneratingAsync(novel.StoryProjectId, taskType);
            await ExtractCharactersAsync(sampledText, novel.StoryProjectId, novelId,
                new AgentRunContext { UserId = userId, ProjectId = novel.StoryProjectId });

            // 3. 世界观提取
            await _progressNotifier.NotifyGeneratingAsync(novel.StoryProjectId, taskType);
            await ExtractWorldRulesAsync(sampledText, novel.StoryProjectId, novelId,
                new AgentRunContext { UserId = userId, ProjectId = novel.StoryProjectId });

            // 4. 文风提取
            await _progressNotifier.NotifyGeneratingAsync(novel.StoryProjectId, taskType);
            await ExtractStyleProfileAsync(sampledText, novel.StoryProjectId, novelId,
                new AgentRunContext { UserId = userId, ProjectId = novel.StoryProjectId });

            await _progressNotifier.NotifyDoneAsync(novel.StoryProjectId, taskType, "资产提取完成，请前往建议中心查看");
            _logger.LogInformation("[AssetExtract] Completed for novel {NovelId}", novelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AssetExtract] Failed for novel {NovelId}", novelId);
            await _progressNotifier.NotifyFailedAsync(novel.StoryProjectId, taskType, "资产提取失败：" + ex.Message);
        }
    }

    // ── 角色提取 ──────────────────────────────────────────────────────────

    private async Task ExtractCharactersAsync(string sampledText, Guid projectId, Guid novelId, AgentRunContext ctx)
    {
        var userPrompt = $"""
            ## 原著片段

            {sampledText}

            请从以上原著片段中识别并提取所有主要角色的信息。
            """;

        var result = await _agentRunner.RunAsync(CharacterExtractAgentDefinition.AgentName, userPrompt, ctx);
        if (!result.Success)
        {
            _logger.LogWarning("[AssetExtract] Character extraction failed: {Error}", result.ErrorMessage);
            return;
        }

        var json = CleanJson(result.Output);
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new NullableIntFromStringConverter() },
        };

        List<CharacterPayload> items;
        try
        {
            items = JsonSerializer.Deserialize<List<CharacterPayload>>(json, opts) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AssetExtract] Failed to parse character JSON");
            return;
        }

        foreach (var item in items.Where(i => !string.IsNullOrWhiteSpace(i.Name)))
        {
            var contentJson = JsonSerializer.Serialize(item, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
            });

            await _suggestionService.CreateAsync(
                agentRunId: ctx.RunId,
                storyProjectId: projectId,
                category: SuggestionCategories.Character,
                title: $"候选角色：{item.Name}",
                contentJson: contentJson,
                sourceNovelId: novelId);
        }

        _logger.LogInformation("[AssetExtract] Extracted {Count} characters for project {ProjectId}",
            items.Count, projectId);
    }

    // ── 世界观提取 ────────────────────────────────────────────────────────

    private async Task ExtractWorldRulesAsync(string sampledText, Guid projectId, Guid novelId, AgentRunContext ctx)
    {
        var userPrompt = $"""
            ## 原著片段

            {sampledText}

            请从以上原著片段中提取对创作有约束性的世界观规则。
            """;

        var result = await _agentRunner.RunAsync(WorldRuleExtractionAgentDefinition.AgentName, userPrompt, ctx);
        if (!result.Success)
        {
            _logger.LogWarning("[AssetExtract] WorldRule extraction failed: {Error}", result.ErrorMessage);
            return;
        }

        var json = CleanJson(result.Output);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        List<WorldRulePayload> items;
        try
        {
            items = JsonSerializer.Deserialize<List<WorldRulePayload>>(json, opts) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AssetExtract] Failed to parse world rule JSON");
            return;
        }

        foreach (var item in items.Where(i => !string.IsNullOrWhiteSpace(i.Title)))
        {
            var contentJson = JsonSerializer.Serialize(item, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
            });

            await _suggestionService.CreateAsync(
                agentRunId: ctx.RunId,
                storyProjectId: projectId,
                category: SuggestionCategories.WorldRule,
                title: $"候选规则：{item.Title}",
                contentJson: contentJson,
                sourceNovelId: novelId);
        }

        _logger.LogInformation("[AssetExtract] Extracted {Count} world rules for project {ProjectId}",
            items.Count, projectId);
    }

    // ── 文风提取 ──────────────────────────────────────────────────────────

    private async Task ExtractStyleProfileAsync(string sampledText, Guid projectId, Guid novelId, AgentRunContext ctx)
    {
        var userPrompt = $"""
            ## 原著片段

            {sampledText}

            请从以上原著片段中归纳该作品的整体文风特征。
            """;

        var result = await _agentRunner.RunAsync(StyleProfileExtractionAgentDefinition.AgentName, userPrompt, ctx);
        if (!result.Success)
        {
            _logger.LogWarning("[AssetExtract] StyleProfile extraction failed: {Error}", result.ErrorMessage);
            return;
        }

        var json = CleanJson(result.Output);
        var contentJson = json; // 直接存储整个 JSON 对象

        await _suggestionService.CreateAsync(
            agentRunId: ctx.RunId,
            storyProjectId: projectId,
            category: SuggestionCategories.StyleProfile,
            title: "候选文风画像",
            contentJson: contentJson,
            sourceNovelId: novelId);

        _logger.LogInformation("[AssetExtract] Extracted style profile for project {ProjectId}", projectId);
    }

    // ── 工具方法 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 从 chunks 中加权采样：前 1/3 取 40%、中 1/3 取 35%、后 1/3 取 25%。
    /// 前段主角出场密集，权重最高。
    /// </summary>
    private static List<string> SampleChunks(List<string> chunks, int targetCount)
    {
        if (chunks.Count <= targetCount) return chunks;

        int total = chunks.Count;
        int frontEnd = total / 3;
        int midEnd = total * 2 / 3;

        int frontCount = (int)Math.Round(targetCount * 0.40);
        int midCount = (int)Math.Round(targetCount * 0.35);
        int tailCount = targetCount - frontCount - midCount;

        static List<string> SampleSegment(List<string> seg, int count)
        {
            if (seg.Count <= count) return seg;
            var res = new List<string>();
            var step = (double)(seg.Count - 1) / (count - 1);
            for (int i = 0; i < count; i++)
                res.Add(seg[(int)Math.Round(i * step)]);
            return res;
        }

        var front = SampleSegment(chunks[..frontEnd], frontCount);
        var mid = SampleSegment(chunks[frontEnd..midEnd], midCount);
        var tail = SampleSegment(chunks[midEnd..], tailCount);

        return [.. front, .. mid, .. tail];
    }

    private static string CleanJson(string raw)
    {
        var json = raw.Trim();
        if (json.StartsWith("```"))
            json = Regex.Replace(json, @"```\w*\n?", "").Trim('`').Trim();
        return json;
    }

    private async Task ApplyUserLlmPreferenceAsync(Guid? userId)
    {
        if (userId is null) return;

        var pref = await _db.UserLlmPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId.Value);

        if (pref is null) return;

        if (Enum.TryParse<LlmProviderType>(pref.Provider, ignoreCase: true, out var provider))
            _selector.Active = provider;

        if (!string.IsNullOrWhiteSpace(pref.ModelId))
            _selector.ActiveModel = pref.ModelId;
    }

    // ── 内部解析模型 ──────────────────────────────────────────────────────

    /// <summary>
    /// LLM 有时把数字字段输出为字符串（如 "20"），此 Converter 兼容两种格式。
    /// </summary>
    private sealed class NullableIntFromStringConverter : System.Text.Json.Serialization.JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType == JsonTokenType.Number) return reader.GetInt32();
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                return int.TryParse(s, out var v) ? v : null;
            }
            return null;
        }
        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        {
            if (value is null) writer.WriteNullValue();
            else writer.WriteNumberValue(value.Value);
        }
    }

    private sealed class CharacterPayload
    {
        public string? Name { get; set; }
        public int? Age { get; set; }
        public string? Role { get; set; }
        public string? PersonalitySummary { get; set; }
        public string? Motivation { get; set; }
        public string? SpeakingStyle { get; set; }
        public string? ForbiddenBehaviors { get; set; }
        public string? CurrentState { get; set; }
    }

    private sealed class WorldRulePayload
    {
        public string? Title { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public int Priority { get; set; } = 3;
        public bool IsHardConstraint { get; set; }
    }
}
