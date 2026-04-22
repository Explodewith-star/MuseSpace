namespace MuseSpace.Application.Abstractions.Llm;

public sealed class DeepSeekOptions
{
    public const string SectionName = "DeepSeek";

    public string BaseUrl { get; init; } = "https://api.deepseek.com/v1";
    public string ApiKey { get; init; } = string.Empty;
    public string ModelName { get; init; } = "deepseek-chat";
}
