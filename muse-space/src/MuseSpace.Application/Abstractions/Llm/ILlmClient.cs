namespace MuseSpace.Application.Abstractions.Llm;

public interface ILlmClient
{
    Task<LlmChatResult> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}
