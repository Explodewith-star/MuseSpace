namespace MuseSpace.Application.Abstractions.Llm;

public sealed class VeniceOptions
{
    public const string SectionName = "Venice";

    public string BaseUrl { get; init; } = "https://api.venice.ai/api/v1";
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>默认模型，当 selector 中没有指定模型时使用。</summary>
    public string ModelName { get; init; } = "venice-uncensored";

    /// <summary>可选模型列表，供前端下拉框展示。</summary>
    public List<ModelOption> AvailableModels { get; init; } = [];
}
