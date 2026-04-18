using MuseSpace.Application.Abstractions.Prompt;

namespace MuseSpace.Infrastructure.Prompt;

public sealed class PromptTemplateRenderer : IPromptTemplateRenderer
{
    public string RenderSystemPrompt(PromptTemplate template, Dictionary<string, string> variables)
    {
        return ReplaceVariables(template.System, variables);
    }

    public string RenderUserPrompt(PromptTemplate template, Dictionary<string, string> variables)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(template.Instruction))
            parts.Add(ReplaceVariables(template.Instruction, variables));

        if (!string.IsNullOrWhiteSpace(template.Context))
            parts.Add(ReplaceVariables(template.Context, variables));

        if (!string.IsNullOrWhiteSpace(template.OutputFormat))
            parts.Add(ReplaceVariables(template.OutputFormat, variables));

        return string.Join("\n\n", parts);
    }

    private static string ReplaceVariables(string text, Dictionary<string, string> variables)
    {
        foreach (var (key, value) in variables)
        {
            text = text.Replace($"{{{{{key}}}}}", value);
        }
        return text;
    }
}
