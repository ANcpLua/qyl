using Qyl.Collector.AgentRuns;
using Qyl.Collector.SchemaControl;

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

        var maxRetainedFailures = config.GetValue("QYL_MAX_BUILD_FAILURES", 10);
        services.AddSingleton<IBuildFailureStore>(_ => new DuckDbBuildFailureStore(dataPath, maxRetainedFailures));

        services.AddSingleton<SchemaPlanner>();
        services.AddSingleton<SchemaExecutor>();

        services.ActivateSingleton<DuckDbStore>();

        services.AddSingleton(sp => new SessionQueryService(sp.GetRequiredService<DuckDbStore>()));
        services.AddSingleton(sp => new AnalyticsQueryService(sp.GetRequiredService<DuckDbStore>()));
        services.AddSingleton(sp => new AgentInsightsService(sp.GetRequiredService<DuckDbStore>()));

        return services;
    }
}
