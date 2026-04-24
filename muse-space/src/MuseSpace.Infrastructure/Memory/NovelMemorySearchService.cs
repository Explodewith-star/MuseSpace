using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Memory;
using Npgsql;
using NpgsqlTypes;

namespace MuseSpace.Infrastructure.Memory;

/// <summary>
/// 基于 pgvector 余弦相似度的原著切片检索服务。
/// 将 queryText 向量化后，与 memory.chunk_embeddings 做 &lt;=&gt; 检索，返回最相关的 TopK 切片。
/// 注意：向量以文本字符串传参（::vector 转换），无需注册 Npgsql pgvector 类型处理器。
/// </summary>
public sealed class NovelMemorySearchService : INovelMemorySearchService
{
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NovelMemorySearchService> _logger;

    public NovelMemorySearchService(
        IEmbeddingClient embeddingClient,
        IConfiguration configuration,
        ILogger<NovelMemorySearchService> logger)
    {
        _embeddingClient = embeddingClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NovelChunkSearchResult>> SearchAsync(
        Guid projectId,
        string queryText,
        int topK = 5,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            return [];

        float[] queryVector;
        try
        {
            queryVector = await _embeddingClient.EmbedAsync(queryText, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding failed during novel search, returning empty results");
            return [];
        }

        // Serialize float[] to PostgreSQL vector string format: [0.1,0.2,...]
        var vectorStr = "[" + string.Join(",",
            queryVector.Select(f => f.ToString("G9", CultureInfo.InvariantCulture))) + "]";

        var connString = _configuration.GetConnectionString("DefaultConnection")!;
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                ce."ChunkId",
                nc."Content",
                nc."ChunkIndex",
                CAST(1.0 - (ce."Embedding" <=> $1::vector) AS float8) AS similarity
            FROM memory.chunk_embeddings ce
            JOIN novel_chunks nc ON nc."Id" = ce."ChunkId"
            WHERE ce."StoryProjectId" = $2
              AND ce."ModelName" = $3
            ORDER BY ce."Embedding" <=> $1::vector
            LIMIT $4
            """;

        cmd.Parameters.Add(new NpgsqlParameter { Value = vectorStr, NpgsqlDbType = NpgsqlDbType.Text });
        cmd.Parameters.Add(new NpgsqlParameter { Value = projectId, NpgsqlDbType = NpgsqlDbType.Uuid });
        cmd.Parameters.Add(new NpgsqlParameter { Value = _embeddingClient.ModelName, NpgsqlDbType = NpgsqlDbType.Varchar });
        cmd.Parameters.Add(new NpgsqlParameter { Value = topK, NpgsqlDbType = NpgsqlDbType.Integer });

        var results = new List<NovelChunkSearchResult>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new NovelChunkSearchResult
            {
                ChunkId = reader.GetGuid(0),
                Content = reader.GetString(1),
                ChunkIndex = reader.GetInt32(2),
                Similarity = reader.GetDouble(3)
            });
        }

        _logger.LogDebug("Novel search returned {Count} results for project {ProjectId}", results.Count, projectId);
        return results;
    }
}
