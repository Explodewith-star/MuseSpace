using MuseSpace.Domain.Entities;

namespace MuseSpace.Infrastructure.Novels;

/// <summary>
/// 将小说全文按固定大小切片，优先在段落或句子边界断开。
/// 参数：目标 1600 字/片，重叠 120 字。
/// </summary>
public sealed class NovelTextChunker
{
    private const int TargetChunkSize = 1600;
    private const int OverlapSize = 120;

    public IReadOnlyList<NovelChunk> Split(string content, Guid novelId, Guid projectId)
    {
        if (string.IsNullOrEmpty(content))
            return [];

        var chunks = new List<NovelChunk>();
        int position = 0;
        int chunkIndex = 0;

        while (position < content.Length)
        {
            position = AlignStartBoundary(content, position);
            int end = Math.Min(position + TargetChunkSize, content.Length);

            // Try to find a natural break point within the last 200 chars
            if (end < content.Length)
                end = FindBreakPoint(content, position, end);

            end = AlignEndBoundary(content, position, end);

            var (trimmedStart, trimmedEnd) = TrimRange(content, position, end);
            if (trimmedEnd > trimmedStart)
            {
                var chunkContent = content[trimmedStart..trimmedEnd];
                chunks.Add(new NovelChunk
                {
                    Id = Guid.NewGuid(),
                    NovelId = novelId,
                    StoryProjectId = projectId,
                    ChunkIndex = chunkIndex++,
                    Content = chunkContent,
                    CharCount = chunkContent.Length,
                    StartOffset = trimmedStart,
                    EndOffset = trimmedEnd,
                    IsEmbedded = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (end >= content.Length)
                break;

            // Advance with overlap to preserve cross-boundary context
            var nextPosition = Math.Max(end - OverlapSize, position + 1);
            position = AlignStartBoundary(content, nextPosition);
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

    private static int AlignStartBoundary(string content, int index)
    {
        if (index <= 0 || index >= content.Length)
            return index;

        return char.IsLowSurrogate(content[index]) && char.IsHighSurrogate(content[index - 1])
            ? index - 1
            : index;
    }

    private static int AlignEndBoundary(string content, int start, int end)
    {
        if (end <= start || end >= content.Length)
            return end;

        return char.IsHighSurrogate(content[end - 1]) && char.IsLowSurrogate(content[end])
            ? end - 1
            : end;
    }

    private static (int Start, int End) TrimRange(string content, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(content[start]))
            start++;

        while (end > start && char.IsWhiteSpace(content[end - 1]))
            end--;

        return (start, end);
    }
}
