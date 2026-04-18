using Microsoft.Extensions.Options;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Logging;
using MuseSpace.Application.Abstractions.Prompt;
using MuseSpace.Application.Abstractions.Skills;
using MuseSpace.Application.Services;
using MuseSpace.Application.Services.Drafting;
using MuseSpace.Infrastructure.Llm;
using MuseSpace.Infrastructure.Logging;
using MuseSpace.Infrastructure.Prompt;
using MuseSpace.Infrastructure.Story;

namespace MuseSpace.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMuseSpaceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // LLM 配置 + 客户端（需要特殊 HttpClient 配置，手动注册）
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.AddHttpClient<ILlmClient, OpenRouterLlmClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
            client.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost");
            client.Timeout = TimeSpan.FromMinutes(3);
        });

        // Prompt（有构造参数，手动注册）
        var promptsPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "prompts"));
        services.AddSingleton<IPromptTemplateProvider>(new FileSystemPromptTemplateProvider(promptsPath));
        services.AddSingleton<IPromptTemplateRenderer, PromptTemplateRenderer>();

        // Data 路径配置（供仓储使用）
        var dataPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data"));
        services.Configure<DataOptions>(opt => opt.BasePath = dataPath);

        // 生成日志（有构造参数，手动注册）
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs", "generations");
        services.AddSingleton<IGenerationLogService>(sp =>
            new GenerationLogService(
                sp.GetRequiredService<ILogger<GenerationLogService>>(),
                logDir));

        // Scrutor：扫描 Application 程序集
        // - ISkill 实现 → 按接口注册（支持多个 Skill 同时注入到 SkillOrchestrator）
        // - ISkillOrchestrator 实现 → 按接口注册
        // - *AppService 类 → 注册为自身（Controller 直接注入）
        services.Scan(scan => scan
            .FromAssemblyOf<SceneDraftSkill>()
            .AddClasses(c => c.AssignableTo<ISkill>())
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(c => c.AssignableTo<ISkillOrchestrator>())
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(c => c.Where(t => t.Name.EndsWith("AppService")))
                .AsSelf()
                .WithScopedLifetime());

        // Scrutor：扫描 Infrastructure 程序集
        // - Json*Repository 类 → 按接口注册
        // - StoryContextBuilder → 按接口注册
        services.Scan(scan => scan
            .FromAssemblyOf<StoryContextBuilder>()
            .AddClasses(c => c.Where(t =>
                t.Name.StartsWith("Json") && t.Name.EndsWith("Repository")))
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(c => c.Where(t => t.Name == nameof(StoryContextBuilder)))
                .AsImplementedInterfaces()
                .WithScopedLifetime());

        return services;
    }
}
