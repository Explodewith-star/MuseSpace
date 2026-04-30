using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MuseSpace.Api.Hangfire;
using MuseSpace.Api.Hubs;
using MuseSpace.Application.Abstractions.Agents;
using MuseSpace.Application.Abstractions.Export;
using MuseSpace.Application.Abstractions.Features;
using MuseSpace.Application.Abstractions.Llm;
using MuseSpace.Application.Abstractions.Logging;
using MuseSpace.Application.Abstractions.Memory;
using MuseSpace.Application.Abstractions.Notifications;
using MuseSpace.Application.Abstractions.Prompt;
using MuseSpace.Application.Abstractions.Skills;
using MuseSpace.Application.Abstractions.Storage;
using MuseSpace.Application.Abstractions.Suggestions;
using MuseSpace.Application.Services;
using MuseSpace.Application.Services.Agents;
using MuseSpace.Application.Services.Drafting;
using MuseSpace.Infrastructure.Agents;
using MuseSpace.Infrastructure.Features;
using MuseSpace.Infrastructure.Llm;
using MuseSpace.Infrastructure.Logging;
using MuseSpace.Infrastructure.Memory;
using MuseSpace.Infrastructure.Novels;
using MuseSpace.Infrastructure.Persistence;
using MuseSpace.Infrastructure.Prompt;
using MuseSpace.Infrastructure.Storage;
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
        services.Configure<DeepSeekOptions>(configuration.GetSection(DeepSeekOptions.SectionName));
        services.Configure<VeniceOptions>(configuration.GetSection(VeniceOptions.SectionName));

        // OpenRouter 客户端（typed HttpClient，不直接绑定 ILlmClient）
        services.AddHttpClient<OpenRouterLlmClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
            client.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost");
            client.Timeout = TimeSpan.FromMinutes(3);
        });

        // DeepSeek 客户端
        services.AddHttpClient<DeepSeekLlmClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<DeepSeekOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
            client.Timeout = TimeSpan.FromMinutes(3);
        });

        // Venice 客户端（仅管理员可用）
        services.AddHttpClient<VeniceLlmClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<VeniceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
            client.Timeout = TimeSpan.FromMinutes(3);
        });

        // 运行时渠道选择器（Scoped：每请求独立，由 LlmPreferenceMiddleware 按用户加载偏好）
        services.AddScoped<LlmProviderSelector>();

        // 路由客户端作为 ILlmClient 的实现
        services.AddScoped<ILlmClient, RoutingLlmClient>();

        // Prompt（有构造参数，手动注册）
        var promptsPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "prompts"));
        services.AddSingleton<IPromptTemplateProvider>(new FileSystemPromptTemplateProvider(promptsPath));
        services.AddSingleton<IPromptTemplateRenderer, PromptTemplateRenderer>();

        // Data 路径配置（供本地文件存储使用，原著原始文件等）
        var dataPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data"));
        services.Configure<DataOptions>(opt => opt.BasePath = dataPath);

        // ── PostgreSQL / EF Core ──────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "未配置数据库连接字符串。请在 appsettings.Development.json 中设置 ConnectionStrings:DefaultConnection。");
        services.AddDbContext<MuseSpaceDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.UseVector()));

        // ── Embedding client (SiliconFlow BAAI/bge-m3) ──────────────────────────
        services.Configure<EmbeddingOptions>(configuration.GetSection(EmbeddingOptions.SectionName));
        services.AddHttpClient<SiliconFlowEmbeddingClient>((sp, client) =>
        {
            var embOpts = sp.GetRequiredService<IOptions<EmbeddingOptions>>().Value;
            client.BaseAddress = new Uri(embOpts.BaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {embOpts.ApiKey}");
            client.Timeout = TimeSpan.FromMinutes(2);
        });
        services.AddScoped<IEmbeddingClient>(sp => sp.GetRequiredService<SiliconFlowEmbeddingClient>());

        // ── Storage service (local file system) ─────────────────────────────────
        services.AddSingleton<IStorageService, LocalStorageService>();

        // ── Novel text chunker (stateless) ──────────────────────────────────────
        services.AddSingleton<NovelTextChunker>();

        // ── Hangfire (PostgreSQL persistent job storage) ─────────────────────────
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));
        services.AddHangfireServer(options => options.WorkerCount = 4);

        // ── SignalR ──────────────────────────────────────────────────────────────
        services.AddSignalR();

        // ── Import progress notifier (SignalR-backed) ────────────────────────────
        services.AddScoped<IImportProgressNotifier, SignalRImportProgressNotifier>();

        // ── Agent progress notifier (SignalR-backed) ─────────────────────────────
        services.AddSingleton<IActiveAgentTaskRegistry, InMemoryActiveAgentTaskRegistry>();
        services.AddScoped<IAgentProgressNotifier, SignalRAgentProgressNotifier>();

        // ── 启动一次性数据迁移（拆分老 Consistency 类目） ──────────────────────
        services.AddHostedService<LegacyConsistencyCategoryMigrationHostedService>();
        // ── 启动 idempotent 建表（plot_threads） ───────────────────────────────
        services.AddHostedService<PlotThreadSchemaInitializerHostedService>();

        // 生成日志（不再写入 JSON 文件，仅 Serilog 结构化日志）
        services.AddSingleton<IGenerationLogService, GenerationLogService>();

        // ── 功能开关（D4-D3） ───────────────────────────────────────────────────
        services.AddMemoryCache();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();

        // ── Agent 运行时 ─────────────────────────────────────────────────────
        // Agent 定义注册（新增 Agent 只需追加一行）
        services.AddSingleton(CharacterExtractAgentDefinition.Create());
        services.AddSingleton(ConsistencyCheckAgentDefinition.Create());
        services.AddSingleton(CharacterConsistencyAgentDefinition.Create());
        services.AddSingleton(OutlinePlanAgentDefinition.Create());
        services.AddSingleton(WorldRuleExtractionAgentDefinition.Create());
        services.AddSingleton(StyleProfileExtractionAgentDefinition.Create());
        services.AddSingleton(ChapterPlanGenerationAgentDefinition.Create());
        services.AddSingleton(StyleConsistencyAgentDefinition.Create());
        services.AddSingleton(ProjectSummaryAgentDefinition.Create());
        services.AddSingleton(PlotThreadTrackingAgentDefinition.Create());
        // Agent 工具注册（P0 暂无工具，P1 扩展时通过 Scrutor 或手动注册 IAgentTool）
        // AgentRunner（Scoped：依赖 DbContext + ILlmClient 都是 Scoped）
        services.AddScoped<IAgentRunner, AgentRunner>();

        // ── Agent 建议审核层 ─────────────────────────────────────────────────
        // ISuggestionApplier 实现按接口注册（新增 Applier 只需追加实现类）
        services.Scan(scan => scan
            .FromAssemblyOf<SceneDraftSkill>()
            .AddClasses(c => c.AssignableTo<ISuggestionApplier>())
                .AsImplementedInterfaces()
                .WithScopedLifetime());

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
                .WithScopedLifetime()
            .AddClasses(c => c.AssignableTo<IChapterExportService>())
                .AsImplementedInterfaces()
                .WithScopedLifetime());

        // Scrutor：扫描 Infrastructure 程序集
        // - Ef*Repository → 按接口注册（替换原有 JSON 仓储）
        // - StoryContextBuilder → 按接口注册
        services.Scan(scan => scan
            .FromAssemblyOf<StoryContextBuilder>()
            .AddClasses(c => c.Where(t =>
                t.Name.StartsWith("Ef") && t.Name.EndsWith("Repository")))
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(c => c.Where(t =>
                t.Name == nameof(StoryContextBuilder) ||
                t.Name == nameof(NovelMemorySearchService)))
                .AsImplementedInterfaces()
                .WithScopedLifetime());

        return services;
    }
}
