using System.Text.Json.Serialization;
using MuseSpace.Infrastructure.Jobs.Internal;

namespace MuseSpace.UnitTests;

public sealed class LlmJsonExtractorTests
{
    private sealed class Payload
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("count")] public int Count { get; set; }
    }

    [Fact]
    public void Clean_StripsJsonCodeFence()
    {
        var raw = "```json\n{\"title\":\"a\",\"count\":3}\n```";
        Assert.Equal("{\"title\":\"a\",\"count\":3}", LlmJsonExtractor.Clean(raw));
    }

    [Fact]
    public void Clean_StripsBareCodeFence()
    {
        var raw = "```\n{\"title\":\"x\"}\n```";
        Assert.Equal("{\"title\":\"x\"}", LlmJsonExtractor.Clean(raw));
    }

    [Fact]
    public void TryDeserialize_ReturnsObject_OnValidJson()
    {
        var raw = "{\"title\":\"hello\",\"count\":5}";
        var obj = LlmJsonExtractor.TryDeserialize<Payload>(raw);
        Assert.NotNull(obj);
        Assert.Equal("hello", obj!.Title);
        Assert.Equal(5, obj.Count);
    }

    [Fact]
    public void TryDeserialize_ReturnsObject_OnFencedJson()
    {
        var raw = "```json\n{\"title\":\"fenced\",\"count\":1}\n```";
        var obj = LlmJsonExtractor.TryDeserialize<Payload>(raw);
        Assert.NotNull(obj);
        Assert.Equal("fenced", obj!.Title);
    }

    [Fact]
    public void TryDeserialize_IsCaseInsensitiveByDefault()
    {
        var raw = "{\"Title\":\"caps\",\"COUNT\":7}";
        var obj = LlmJsonExtractor.TryDeserialize<Payload>(raw);
        Assert.NotNull(obj);
        Assert.Equal("caps", obj!.Title);
        Assert.Equal(7, obj.Count);
    }

    [Fact]
    public void TryDeserialize_ReturnsNull_OnMalformedJson()
    {
        Assert.Null(LlmJsonExtractor.TryDeserialize<Payload>("not json at all"));
        Assert.Null(LlmJsonExtractor.TryDeserialize<Payload>("```json\nnot json\n```"));
        Assert.Null(LlmJsonExtractor.TryDeserialize<Payload>(""));
    }
}
