using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Novels;

/// <summary>
/// 将小说全文按固定大小切片，优先在段落或句子边界断开。
/// 参数：目标 800 字/片，重叠 80 字。
/// </summary>
public sealed class NovelTextChunker
{
    private const int TargetChunkSize = 800;
    private const int OverlapSize = 80;

    public IReadOnlyList<NovelChunk> Split(string content, Guid novelId, Guid projectId)
    {
        if (string.IsNullOrEmpty(content))
            return [];

        var chunks = new List<NovelChunk>();
        int position = 0;
        int chunkIndex = 0;

        while (position < content.Length)
        {
            int end = Math.Min(position + TargetChunkSize, content.Length);

            // Try to find a natural break point within the last 200 chars
            if (end < content.Length)
                end = FindBreakPoint(content, position, end);

            var chunkContent = content[position..end].Trim();
            if (!string.IsNullOrWhiteSpace(chunkContent))
            {
                chunks.Add(new NovelChunk
                {
                    Id = Guid.NewGuid(),
                    NovelId = novelId,
                    StoryProjectId = projectId,
                    ChunkIndex = chunkIndex++,
                    Content = chunkContent,
                    CharCount = chunkContent.Length,
                    StartOffset = position,
                    EndOffset = end,
                    IsEmbedded = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Advance with overlap to preserve cross-boundary context
            position = Math.Max(end - OverlapSize, position + 1);
        }

        return chunks;
    }

    private static int FindBreakPoint(string content, int start, int preferredEnd)
    {
        int searchStart = Math.Max(start, preferredEnd - 200);

        // Priority 1: paragraph break (\n\n)
        int idx = content.LastIndexOf("\n\n", preferredEnd, preferredEnd - searchStart,
            StringComparison.Ordinal);
        if (idx > searchStart) return idx + 2;

        // Priority 2: Chinese/English sentence-ending punctuation or newline
        for (int i = preferredEnd - 1; i >= searchStart; i--)
        {
            if (content[i] is '。' or '！' or '？' or '…' or '\n' or '.' or '!' or '?')
                return i + 1;
        }

        return preferredEnd;
    }
}
