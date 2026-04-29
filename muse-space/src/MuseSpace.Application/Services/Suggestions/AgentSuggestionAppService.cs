using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Application.Abstractions.Suggestions;
using MuseSpace.Contracts.Suggestions;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

namespace MuseSpace.Application.Services.Suggestions;

public sealed class AgentSuggestionAppService
{
    private readonly IAgentSuggestionRepository _repository;
    private readonly Dictionary<string, ISuggestionApplier> _appliers;

    public AgentSuggestionAppService(
        IAgentSuggestionRepository repository,
        IEnumerable<ISuggestionApplier> appliers)
    {
        _repository = repository;
        _appliers = appliers.ToDictionary(a => a.Category, StringComparer.OrdinalIgnoreCase);
    }

    // ── 创建建议 ──────────────────────────────────────────────────────────────

    public async Task<AgentSuggestionResponse> CreateAsync(
        Guid agentRunId,
        Guid storyProjectId,
        string category,
        string title,
        string contentJson,
        Guid? targetEntityId = null,
        Guid? sourceNovelId = null,
        CancellationToken cancellationToken = default)
    {
        var suggestion = new AgentSuggestion
        {
            AgentRunId = agentRunId,
            StoryProjectId = storyProjectId,
            Category = category,
            Title = title,
            ContentJson = contentJson,
            TargetEntityId = targetEntityId,
            SourceNovelId = sourceNovelId,
        };

        await _repository.AddAsync(suggestion, cancellationToken);
        return ToResponse(suggestion);
    }

    // ── 查询 ──────────────────────────────────────────────────────────────────

    public async Task<List<AgentSuggestionResponse>> GetByProjectAsync(
        Guid projectId, string? category = null, SuggestionStatus? status = null,
        Guid? targetEntityId = null, CancellationToken cancellationToken = default)
    {
        var list = await _repository.GetByProjectAsync(projectId, category, status, targetEntityId, cancellationToken);
        return list.Select(ToResponse).ToList();
    }

    public async Task<AgentSuggestionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetByIdAsync(id, cancellationToken);
        return s is null ? null : ToResponse(s);
    }

    public async Task<List<AgentSuggestionResponse>> GetByAgentRunAsync(Guid agentRunId, CancellationToken cancellationToken = default)
    {
        var list = await _repository.GetByAgentRunAsync(agentRunId, cancellationToken);
        return list.Select(ToResponse).ToList();
    }

    // ── 状态流转 ──────────────────────────────────────────────────────────────

    public async Task<AgentSuggestionResponse?> AcceptAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetByIdAsync(id, cancellationToken);
        if (s is null || s.Status != SuggestionStatus.Pending) return null;

        s.Status = SuggestionStatus.Accepted;
        s.ResolvedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(s, cancellationToken);
        return ToResponse(s);
    }

    public async Task<bool> IgnoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetByIdAsync(id, cancellationToken);
        if (s is null) return false;

        if (s.Status == SuggestionStatus.Pending)
        {
            // Pending → 标记为已忽略（保留建议记录，无对应资产需清理）
            s.Status = SuggestionStatus.Ignored;
            s.ResolvedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(s, cancellationToken);
            return true;
        }

        if (s.Status == SuggestionStatus.Applied)
        {
            // Applied → 删除对应资产，再标记为已忽略
            if (s.TargetEntityId.HasValue && _appliers.TryGetValue(s.Category, out var applier))
                await applier.RetractAsync(s, cancellationToken);

            s.Status = SuggestionStatus.Ignored;
            s.ResolvedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(s, cancellationToken);
            return true;
        }

        return false;
    }

    /// <summary>将已忽略的建议重新写入资产表，标记为已应用。</summary>
    public async Task<AgentSuggestionResponse?> ReApplyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetByIdAsync(id, cancellationToken);
        if (s is null || s.Status != SuggestionStatus.Ignored) return null;

        if (_appliers.TryGetValue(s.Category, out var applier))
        {
            // 重新执行 Applier 将资产 upsert 回正式表（使用原 TargetEntityId）
            var entityId = await applier.ApplyAsync(s, cancellationToken);
            s.TargetEntityId = entityId;
        }

        s.Status = SuggestionStatus.Applied;
        s.ResolvedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(s, cancellationToken);
        return ToResponse(s);
    }

    /// <summary>物理删除任意状态的建议记录。</summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var exists = await _repository.GetByIdAsync(id, cancellationToken);
        if (exists is null) return false;
        await _repository.DeleteAsync(id, cancellationToken);
        return true;
    }

    /// <summary>更新建议正文 JSON（允许任意状态，用于大纲编辑后保存）。</summary>
    public async Task<AgentSuggestionResponse?> UpdateContentAsync(Guid id, string contentJson, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetByIdAsync(id, cancellationToken);
        if (s is null) return null;
        s.ContentJson = contentJson;
        await _repository.UpdateAsync(s, cancellationToken);
        return ToResponse(s);
    }

    /// <summary>将已接受的建议写入正式业务表。</summary>
    public async Task<AgentSuggestionResponse?> ApplyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _repository.GetByIdAsync(id, cancellationToken);
        if (s is null || s.Status != SuggestionStatus.Accepted) return null;

        if (!_appliers.TryGetValue(s.Category, out var applier))
            throw new InvalidOperationException($"未找到 Category='{s.Category}' 对应的 Applier");

        var entityId = await applier.ApplyAsync(s, cancellationToken);

        s.Status = SuggestionStatus.Applied;
        s.TargetEntityId = entityId;
        await _repository.UpdateAsync(s, cancellationToken);
        return ToResponse(s);
    }

    /// <summary>批量操作：QuickApply / Ignore / Delete / ReApply。</summary>
    public async Task<int> BatchResolveAsync(List<Guid> ids, string action, CancellationToken cancellationToken = default)
    {
        if (action.Equals("Delete", StringComparison.OrdinalIgnoreCase))
        {
            // 物理删除（仅已忽略的建议才允许调用此操作）
            return await _repository.DeleteByIdsAsync(ids, cancellationToken);
        }

        if (action.Equals("Ignore", StringComparison.OrdinalIgnoreCase))
        {
            // Pending → 已忽略（保留记录）；Applied → 先删除资产再已忽略
            var suggestions = await _repository.GetByIdsAsync(ids, cancellationToken);
            var toIgnore = suggestions
                .Where(s => s.Status == SuggestionStatus.Pending || s.Status == SuggestionStatus.Applied)
                .ToList();
            if (toIgnore.Count == 0) return 0;
            foreach (var s in toIgnore)
            {
                if (s.Status == SuggestionStatus.Applied && s.TargetEntityId.HasValue
                    && _appliers.TryGetValue(s.Category, out var applier))
                    await applier.RetractAsync(s, cancellationToken);

                s.Status = SuggestionStatus.Ignored;
                s.ResolvedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(s, cancellationToken);
            }
            return toIgnore.Count;
        }

        if (action.Equals("ReApply", StringComparison.OrdinalIgnoreCase))
        {
            // 将已忽略的建议重新写入资产表 + 标记为已应用
            var suggestions = await _repository.GetByIdsAsync(ids, cancellationToken);
            var count = 0;
            foreach (var s in suggestions.Where(s => s.Status == SuggestionStatus.Ignored))
            {
                if (_appliers.TryGetValue(s.Category, out var applier))
                {
                    var entityId = await applier.ApplyAsync(s, cancellationToken);
                    s.TargetEntityId = entityId;
                }
                s.Status = SuggestionStatus.Applied;
                s.ResolvedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(s, cancellationToken);
                count++;
            }
            return count;
        }

        if (action.Equals("QuickApply", StringComparison.OrdinalIgnoreCase))
        {
            // 一步导入到正式内容：Pending → Accepted → Applied
            var suggestions = await _repository.GetByIdsAsync(ids, cancellationToken);
            var count = 0;
            foreach (var s in suggestions.Where(s => s.Status == SuggestionStatus.Pending))
            {
                // 没有 Applier 的类型（如大纲）跳过，只接受不应用
                if (!_appliers.TryGetValue(s.Category, out var applier))
                {
                    s.Status = SuggestionStatus.Accepted;
                    s.ResolvedAt = DateTime.UtcNow;
                    await _repository.UpdateAsync(s, cancellationToken);
                    count++;
                    continue;
                }

                s.Status = SuggestionStatus.Accepted;
                s.ResolvedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(s, cancellationToken);

                var entityId = await applier.ApplyAsync(s, cancellationToken);
                s.Status = SuggestionStatus.Applied;
                s.TargetEntityId = entityId;
                await _repository.UpdateAsync(s, cancellationToken);
                count++;
            }
            return count;
        }

        if (action.Equals("Accept", StringComparison.OrdinalIgnoreCase))
        {
            // 兼容旧调用：仅移动到 Accepted 状态
            var suggestions = await _repository.GetByIdsAsync(ids, cancellationToken);
            var count = 0;
            foreach (var s in suggestions.Where(s => s.Status == SuggestionStatus.Pending))
            {
                s.Status = SuggestionStatus.Accepted;
                s.ResolvedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(s, cancellationToken);
                count++;
            }
            return count;
        }

        throw new ArgumentException($"不支持的操作: {action}，仅支持 QuickApply / Ignore / Delete / Accept / ReApply");
    }

    // ── 映射 ──────────────────────────────────────────────────────────────────

    private static AgentSuggestionResponse ToResponse(AgentSuggestion s) => new()
    {
        Id = s.Id,
        AgentRunId = s.AgentRunId,
        StoryProjectId = s.StoryProjectId,
        Category = s.Category,
        Title = s.Title,
        ContentJson = s.ContentJson,
        Status = s.Status.ToString(),
        TargetEntityId = s.TargetEntityId,
        SourceNovelId = s.SourceNovelId,
        CreatedAt = s.CreatedAt,
        ResolvedAt = s.ResolvedAt,
    };
}
