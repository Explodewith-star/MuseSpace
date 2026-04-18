using System.Text;
using System.Text.RegularExpressions;
using MuseSpace.Application.Abstractions.Prompt;

namespace MuseSpace.Infrastructure.Prompt;

public sealed class FileSystemPromptTemplateProvider : IPromptTemplateProvider
{
    private readonly string _basePath;

    public FileSystemPromptTemplateProvider(string basePath)
    {
        _basePath = basePath;
    }

    public async Task<PromptTemplate> GetTemplateAsync(
        string category,
        string name,
        CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_basePath, category, $"{name}.md");

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Prompt template not found: {filePath}");

        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return ParseTemplate(content, category, name);
    }

    private static PromptTemplate ParseTemplate(string content, string category, string name)
    {
        var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var currentSection = string.Empty;
        var currentContent = new StringBuilder();

        foreach (var line in content.Split('\n'))
        {
            var match = Regex.Match(line.Trim(), @"^##\s+(\w+)\s*$");
            if (match.Success)
            {
                if (!string.IsNullOrEmpty(currentSection))
                    sections[currentSection] = currentContent.ToString().Trim();

                currentSection = match.Groups[1].Value;
                currentContent.Clear();
            }
            else
            {
                currentContent.AppendLine(line);
            }
        }

        if (!string.IsNullOrEmpty(currentSection))
            sections[currentSection] = currentContent.ToString().Trim();

        var versionMatch = Regex.Match(name, @"-v(\d+)$");
        var version = versionMatch.Success ? $"v{versionMatch.Groups[1].Value}" : "v1";

        return new PromptTemplate
        {
            Name = $"{category}/{name}",
            Version = version,
            System = sections.GetValueOrDefault("system", string.Empty),
            Instruction = sections.GetValueOrDefault("instruction", string.Empty),
            Context = sections.GetValueOrDefault("context", string.Empty),
            OutputFormat = sections.GetValueOrDefault("output_format", string.Empty)
        };
    }
}
