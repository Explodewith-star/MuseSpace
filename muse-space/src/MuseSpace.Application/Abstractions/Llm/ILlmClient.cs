namespace MuseSpace.Application.Abstractions.Llm;

public interface ILlmClient
{
    Task<string> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}
