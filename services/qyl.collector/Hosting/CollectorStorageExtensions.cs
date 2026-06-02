using Qyl.Collector.Intelligence;

namespace Qyl.Collector.Hosting;

public static class CollectorStorageExtensions
{
    private const string DefaultDataPath = "qyl.duckdb";

    public static IServiceCollection AddQylCollectorStorage(this IServiceCollection services)
    {
        services.AddSingleton(CreateStore);
        services.ActivateSingleton<DuckDbStore>();

        services.AddSingleton(static sp => new SessionQueryService(sp.GetRequiredService<DuckDbStore>()));
        services.AddSingleton(static sp => new AnalyticsQueryService(sp.GetRequiredService<DuckDbStore>()));

        services.AddSingleton<IPatternEngine>(
            new PatternEngine(DiagnosticPatternCatalog.Patterns, DiagnosticPatternCatalog.CausalRules,
                DiagnosticPatternCatalog.InvestigationStrategies));

        return services;
    }

    private static DuckDbStore CreateStore(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var dataPath = config["QYL_DATA_PATH"] ?? DefaultDataPath;
        var dataDir = Path.GetDirectoryName(dataPath);
        if (!string.IsNullOrEmpty(dataDir))
            Directory.CreateDirectory(dataDir);

        return new DuckDbStore(dataPath);
    }
}
