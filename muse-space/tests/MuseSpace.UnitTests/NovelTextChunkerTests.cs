using MuseSpace.Infrastructure.Novels;

namespace MuseSpace.UnitTests;

public sealed class NovelTextChunkerTests
{
    private readonly NovelTextChunker _chunker = new();

    [Fact]
    public void Split_DoesNotEndChunkInsideSurrogatePair()
    {
        var content = new string('甲', 799) + "😀" + new string('乙', 40);

        var chunks = _chunker.Split(content, Guid.NewGuid(), Guid.NewGuid());

        Assert.NotEmpty(chunks);
        AssertAllChunksHaveValidUnicode(chunks.Select(c => c.Content));
        Assert.Contains(chunks, chunk => chunk.Content.Contains("😀", StringComparison.Ordinal));
    }

    [Fact]
    public void Split_DoesNotStartOverlapInsideSurrogatePair()
    {
        var content = new string('甲', 719) + "😀" + new string('乙', 200);

        var chunks = _chunker.Split(content, Guid.NewGuid(), Guid.NewGuid());

        Assert.True(chunks.Count >= 2);
        AssertAllChunksHaveValidUnicode(chunks.Select(c => c.Content));
        Assert.StartsWith("😀", chunks[1].Content, StringComparison.Ordinal);
    }

    private static void AssertAllChunksHaveValidUnicode(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            Assert.False(HasInvalidUnicodeScalar(value), $"Chunk contains invalid Unicode: {value}");
        }
    }

    private static bool HasInvalidUnicodeScalar(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                    return true;

                i++;
                continue;
            }

            if (char.IsLowSurrogate(value[i]))
                return true;
        }

        return false;
    }
}