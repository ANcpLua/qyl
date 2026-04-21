using Qyl.Collector.AgentRuns;
using Qyl.Collector.Intelligence;
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
        services.ActivateSingleton<DuckDbStore>();

        // Plain singletons (MigrationRunner, SourceLocationCache, PdbSourceResolver,
        // SchemaPlanner, SchemaExecutor, ModelPricingService) auto-register via
        // [QylService(Singleton)] through QylGeneratedRegistry.RegisterQylServices.
        // Query-service factories stay here — they need sp.GetRequiredService<DuckDbStore>().
        services.AddSingleton(sp => new SessionQueryService(sp.GetRequiredService<DuckDbStore>()));
        services.AddSingleton(sp => new AnalyticsQueryService(sp.GetRequiredService<DuckDbStore>()));
        services.AddSingleton(sp => new AgentInsightsService(sp.GetRequiredService<DuckDbStore>()));

        services.AddSingleton<IPatternEngine>(
            new PatternEngine(DiagnosticPatterns.All, CausalRules.All, InvestigationStrategies.All));

        return services;
    }
}
