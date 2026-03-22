using Qyl.Collector.AgentRuns;
using Qyl.Collector.Cost;
using Qyl.Collector.Intelligence;
using Qyl.Collector.SchemaControl;
using Qyl.Contracts.Intelligence;

namespace Qyl.Collector.Hosting;

public static class CollectorStorageExtensions
{
    public static IServiceCollection AddQylCollectorStorage(
        this IServiceCollection services,
        IConfiguration config)
    {
        var dataPath = config["QYL_DATA_PATH"] ?? "qyl.duckdb";
        var dataDir = Path.GetDirectoryName(dataPath);
        if (!string.IsNullOrEmpty(dataDir))
            Directory.CreateDirectory(dataDir);

        services.AddSingleton(_ => new DuckDbStore(dataPath));
        services.AddSingleton<MigrationRunner>();
        services.AddSingleton<SourceLocationCache>();
        services.AddSingleton<PdbSourceResolver>();

        services.AddSingleton<SchemaPlanner>();
        services.AddSingleton<SchemaExecutor>();

        services.ActivateSingleton<DuckDbStore>();

        services.AddSingleton<ModelPricingService>();

        services.AddSingleton(sp => new SessionQueryService(sp.GetRequiredService<DuckDbStore>()));
        services.AddSingleton(sp => new AnalyticsQueryService(sp.GetRequiredService<DuckDbStore>()));
        services.AddSingleton(sp => new AgentInsightsService(sp.GetRequiredService<DuckDbStore>()));

        services.AddSingleton<IPatternEngine>(
            new PatternEngine(DiagnosticPatterns.All, CausalRules.All, InvestigationStrategies.All));

        return services;
    }
}
