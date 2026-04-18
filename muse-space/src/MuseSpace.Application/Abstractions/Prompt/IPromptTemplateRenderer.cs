namespace MuseSpace.Application.Abstractions.Prompt;

public interface IPromptTemplateRenderer
{
    string RenderSystemPrompt(PromptTemplate template, Dictionary<string, string> variables);
    string RenderUserPrompt(PromptTemplate template, Dictionary<string, string> variables);
}
