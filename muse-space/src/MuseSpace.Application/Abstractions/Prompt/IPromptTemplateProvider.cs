namespace MuseSpace.Application.Abstractions.Prompt;

public interface IPromptTemplateProvider
{
    Task<PromptTemplate> GetTemplateAsync(
        string category,
        string name,
        CancellationToken cancellationToken = default);
}
