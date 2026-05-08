using System.Text.Json;
using Hangfire;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Agents;
using MuseSpace.Application.Abstractions.Features;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Domain.Entities;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Infrastructure.Jobs;

/// <summary>
/// Hangfire Job（Module E+）：在原著导入完成后自动生成：
///   1. 原著大结局摘要 → 写入 Novel.EndingSummary
///   2. 原著文风摘要   → 写入 Novel.StyleSummary
///   3. 主要角色结尾状态快照 → 写入 novel_character_snapshots 表
///
/// 由 ExtractNovelAssetsJob 完成后链式触发，确保 chunk 已可用。
/// 取最后 10% 的 chunk（至多 12 个）作为结局分析输入，
/// 取前 5 个 chunk 作为文风分析输入。
/// </summary>
public sealed class NovelEndingSummaryJob
{
    private const int MaxTailChunks = 12;
    private const int HeadChunksForStyle = 5;

    private readonly INovelRepository _novelRepo;
    private readonly INovelChunkRepository _chunkRepo;
    private readonly INovelCharacterSnapshotRepository _snapshotRepo;
    private readonly IAgentRunner _agentRunner;
    private readonly ITaskProgressService _taskProgress;
    private readonly LlmProviderSelector _selector;
    private readonly MuseSpaceDbContext _db;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<NovelEndingSummaryJob> _logger;

    public NovelEndingSummaryJob(
        INovelRepository novelRepo,
        INovelChunkRepository chunkRepo,
        INovelCharacterSnapshotRepository snapshotRepo,
        IAgentRunner agentRunner,
        ITaskProgressService taskProgress,
        LlmProviderSelector selector,
        MuseSpaceDbContext db,
        IFeatureFlagService featureFlags,
        ILogger<NovelEndingSummaryJob> logger)
    {
        _novelRepo = novelRepo;
        _chunkRepo = chunkRepo;
        _snapshotRepo = snapshotRepo;
        _agentRunner = agentRunner;
        _taskProgress = taskProgress;
        _selector = selector;
        _db = db;
        _featureFlags = featureFlags;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task ExecuteAsync(Guid novelId, Guid? userId, PerformContext? context = null)
    {
        if (!await _featureFlags.IsEnabledAsync(FeatureFlagKeys.AutoNovelEndingSummary, defaultValue: true))
        {
            _logger.LogInformation("[NovelEndingSummary] Skipped by feature flag for novel {NovelId}", novelId);
            return;
        }

        var novel = await _novelRepo.GetByIdAsync(novelId);
        if (novel is null)
        {
            _logger.LogWarning("[NovelEndingSummary] Novel {NovelId} not found", novelId);
            return;
        }

        _logger.LogInformation("[NovelEndingSummary] Start for novel {NovelId} ({Title})", novelId, novel.Title);

        var bgTaskId = await _taskProgress.StartAsync(
            userId, null,
            Domain.Enums.BackgroundTaskType.NovelEndingSummary,
            $"结局分析《{novel.Title}》");

        try
        {
            if (userId is not null)
            {
                var pref = await _db.UserLlmPreferences.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == userId.Value);
                if (pref is not null)
                {
                    if (Enum.TryParse<LlmProviderType>(pref.Provider, ignoreCase: true, out var provider))
                        _selector.Active = provider;
                    if (!string.IsNullOrWhiteSpace(pref.ModelId))
                        _selector.ActiveModel = pref.ModelId;
                }
            }

            var allChunks = (await _chunkRepo.GetByNovelAsync(novelId))
                .OrderBy(c => c.ChunkIndex)
                .ToList();

            if (allChunks.Count == 0)
            {
                _logger.LogWarning("[NovelEndingSummary] No chunks for novel {NovelId}, skip", novelId);
                await _taskProgress.FailAsync(bgTaskId, "无可用的文本切片");
                return;
            }

            // ── 取尾部 chunk（结局分析） ───────────────────────────────────────
            int tailCount = Math.Min(MaxTailChunks, Math.Max(1, (int)(allChunks.Count * 0.10)));
            var tailChunks = allChunks.TakeLast(tailCount).ToList();
            var tailText = string.Join("\n\n", tailChunks.Select(c => c.Content));

            // ── 取头部 chunk（文风分析） ───────────────────────────────────────
            var headText = string.Join("\n\n", allChunks.Take(HeadChunksForStyle).Select(c => c.Content));

            // ── Agent 运行：结局摘要 + 角色末态 ──────────────────────────────
            var agentCtx = new AgentRunContext
            {
                UserId = userId,
                ProjectId = null,
            };

            var endingPrompt = BuildEndingPrompt(novel.Title, tailText);
            await _taskProgress.ReportProgressAsync(bgTaskId, 20, "正在分析结局...");
            var endingResult = await _agentRunner.RunAsync("novel-ending-summary", endingPrompt, agentCtx);

            string? endingSummary = null;
            var characterSnapshots = new List<NovelCharacterSnapshot>();

            if (endingResult.Success && !string.IsNullOrWhiteSpace(endingResult.Output))
            {
                ParseEndingOutput(endingResult.Output, novelId, novel.StoryProjectId,
                    out endingSummary, out characterSnapshots);
            }
            else
            {
                _logger.LogWarning("[NovelEndingSummary] Agent failed for novel {NovelId}: {Err}",
                    novelId, endingResult.ErrorMessage);
            }

            // ── Agent 运行：文风摘要 ───────────────────────────────────────────
            string? styleSummary = null;
            var stylePrompt = BuildStylePrompt(novel.Title, headText);
            await _taskProgress.ReportProgressAsync(bgTaskId, 50, "正在分析文风...");
            var styleResult = await _agentRunner.RunAsync("novel-style-summary", stylePrompt, agentCtx);
            if (styleResult.Success && !string.IsNullOrWhiteSpace(styleResult.Output))
                styleSummary = ExtractTextField(styleResult.Output, "style_summary");

            // ── 写回 Novel ────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(endingSummary) || !string.IsNullOrWhiteSpace(styleSummary))
            {
                novel.EndingSummary = endingSummary ?? novel.EndingSummary;
                novel.StyleSummary = styleSummary ?? novel.StyleSummary;
                novel.SummaryGeneratedAt = DateTime.UtcNow;
                novel.UpdatedAt = DateTime.UtcNow;
                await _novelRepo.UpdateAsync(novel);
                _logger.LogInformation("[NovelEndingSummary] Novel {NovelId} summary written", novelId);
            }

            // ── 写入角色快照（先删旧的再插入） ────────────────────────────────
            if (characterSnapshots.Count > 0)
            {
                await _snapshotRepo.DeleteByNovelAsync(novelId);
                await _snapshotRepo.AddRangeAsync(characterSnapshots);
                _logger.LogInformation("[NovelEndingSummary] {Count} character snapshots saved for novel {NovelId}",
                    characterSnapshots.Count, novelId);
            }

            await _taskProgress.CompleteAsync(bgTaskId, "结局分析完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NovelEndingSummary] Failed for novel {NovelId}", novelId);
            await _taskProgress.FailAsync(bgTaskId, "结局分析失败：" + ex.Message);
        }
    }

    // ── 内联 Prompt 构建 ───────────────────────────────────────────────────

    private static string BuildEndingPrompt(string title, string tailText) => $$"""
        你是一名专业文学分析师。以下是小说《{{title}}》结尾部分的原文片段：

        ---
        {{tailText}}
        ---

        请完成两项任务：

        **任务1：大结局摘要**
        用 200-350 字概括主要人物的最终走向与结局，包括：
        - 主要人物的命运（存活/死亡/离开/和解等）
        - 核心关系的最终状态（在一起/分离/对立/和解）
        - 世界/社会层面的收束（如果有）
        禁止发挥想象，只能基于原文内容。

        **任务2：主要角色结尾状态**
        提取最多 8 个重要角色，每个角色输出一行状态描述（20-60字），
        标记是否为不可逆状态（死亡、永久分离、关键身份转变等）。

        请以以下 JSON 格式输出（不要包含 markdown 代码块）：
        {"ending_summary":"...", "characters":[{"name":"角色名","state":"状态描述","irreversible":true}]}
        """;

    private static string BuildStylePrompt(string title, string headText) => $$"""
        你是一名专业文学编辑。以下是小说《{{title}}》开头部分的原文片段：

        ---
        {{headText}}
        ---

        请用 80-150 字描述该小说的文风特征，包括：
        - 语调（如：沉郁压抑 / 明快灵动 / 冷静克制）
        - 句式偏好（如：长句为主、短句为主、长短交替）
        - 描写密度（如：重心理描写、重场景白描）
        - 对话特点（如：含蓄克制 / 直白犀利 / 大量内心独白）

        以 JSON 格式输出（不要包含 markdown 代码块）：
        {"style_summary":"..."}
        """;

    // ── 输出解析 ──────────────────────────────────────────────────────────

    private static void ParseEndingOutput(
        string raw,
        Guid novelId,
        Guid projectId,
        out string? endingSummary,
        out List<NovelCharacterSnapshot> snapshots)
    {
        endingSummary = null;
        snapshots = [];

        try
        {
            var json = StripCodeFence(raw);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("ending_summary", out var summaryEl))
                endingSummary = summaryEl.GetString();

            if (root.TryGetProperty("characters", out var charsEl) &&
                charsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in charsEl.EnumerateArray())
                {
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var state = item.TryGetProperty("state", out var s) ? s.GetString() : null;
                    var irreversible = item.TryGetProperty("irreversible", out var ir) && ir.GetBoolean();

                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(state))
                    {
                        snapshots.Add(new NovelCharacterSnapshot
                        {
                            NovelId = novelId,
                            StoryProjectId = projectId,
                            CharacterName = name!,
                            EndingState = state!,
                            IsIrreversible = irreversible,
                        });
                    }
                }
            }
        }
        catch
        {
            // 解析失败：endingSummary = null，snapshots = empty，后续静默降级
        }
    }

    private static string? ExtractTextField(string raw, string key)
    {
        try
        {
            var json = StripCodeFence(raw);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(key, out var el))
                return el.GetString();
        }
        catch { }
        return null;
    }

    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;
        var firstNewLine = trimmed.IndexOf('\n');
        if (firstNewLine < 0) return trimmed;
        var withoutFirstLine = trimmed[(firstNewLine + 1)..];
        var lastFence = withoutFirstLine.LastIndexOf("```");
        return lastFence > 0 ? withoutFirstLine[..lastFence].Trim() : withoutFirstLine.Trim();
    }
}
