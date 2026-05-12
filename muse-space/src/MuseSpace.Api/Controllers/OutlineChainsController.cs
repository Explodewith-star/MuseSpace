using Microsoft.AspNetCore.Mvc;
using MuseSpace.Application.Abstractions.Repositories;
using MuseSpace.Contracts.Common;
using MuseSpace.Contracts.Outlines;
using MuseSpace.Domain.Entities;
using MuseSpace.Domain.Enums;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/chains")]
public sealed class OutlineChainsController : ControllerBase
{
    private readonly IOutlineChainRepository _chainRepo;
    private readonly IStoryOutlineRepository _outlineRepo;

    public OutlineChainsController(
        IOutlineChainRepository chainRepo,
        IStoryOutlineRepository outlineRepo)
    {
        _chainRepo = chainRepo;
        _outlineRepo = outlineRepo;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<OutlineChainResponse>>>> GetAll(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var chains = await _chainRepo.GetByProjectAsync(projectId, cancellationToken);
        var outlines = await _outlineRepo.GetByProjectAsync(projectId, cancellationToken);
        var countByChain = outlines
            .Where(o => o.ChainId.HasValue)
            .GroupBy(o => o.ChainId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = chains.Select(c => new OutlineChainResponse
        {
            Id = c.Id,
            StoryProjectId = c.StoryProjectId,
            Name = c.Name,
            Mode = c.Mode.ToString(),
            DisplayOrder = c.DisplayOrder,
            CreatedAt = c.CreatedAt,
            OutlineCount = countByChain.GetValueOrDefault(c.Id),
        }).ToList();

        return Ok(ApiResponse<List<OutlineChainResponse>>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<OutlineChainResponse>>> Create(
        Guid projectId,
        [FromBody] CreateOutlineChainRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await _chainRepo.GetByProjectAsync(projectId, cancellationToken);

        var chain = new OutlineChain
        {
            Id = Guid.NewGuid(),
            StoryProjectId = projectId,
            Name = string.IsNullOrWhiteSpace(request.Name)
                ? BuildDefaultName(request.Mode)
                : request.Name.Trim(),
            Mode = Enum.TryParse<GenerationMode>(request.Mode, true, out var m) ? m : GenerationMode.Original,
            DisplayOrder = existing.Count,
            CreatedAt = DateTime.UtcNow,
        };

        await _chainRepo.SaveAsync(chain, cancellationToken);

        return Ok(ApiResponse<OutlineChainResponse>.Ok(new OutlineChainResponse
        {
            Id = chain.Id,
            StoryProjectId = chain.StoryProjectId,
            Name = chain.Name,
            Mode = chain.Mode.ToString(),
            DisplayOrder = chain.DisplayOrder,
            CreatedAt = chain.CreatedAt,
            OutlineCount = 0,
        }));
    }

    [HttpDelete("{chainId:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid projectId,
        Guid chainId,
        CancellationToken cancellationToken)
    {
        var chain = await _chainRepo.GetByIdAsync(chainId, cancellationToken);
        if (chain is null || chain.StoryProjectId != projectId)
            return NotFound(ApiResponse<bool>.Fail("Chain not found"));

        await _chainRepo.DeleteAsync(chainId, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(true));
    }

    private static string BuildDefaultName(string? mode)
        => Enum.TryParse<GenerationMode>(mode, true, out var m)
            ? m switch
            {
                GenerationMode.ContinueFromOriginal => "原著续写线",
                GenerationMode.SideStoryFromOriginal => "支线番外线",
                GenerationMode.ExpandOrRewrite => "扩写改写线",
                _ => "原创主线",
            }
            : "原创主线";
}
