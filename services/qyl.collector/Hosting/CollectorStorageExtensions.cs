using Qyl.Collector.Health;

namespace Qyl.Collector.Hosting;

internal static class CollectorStorageExtensions
{
    private const string DefaultDataPath = "qyl.duckdb";

    public static IServiceCollection AddQylCollectorStorage(this IServiceCollection services)
    {
        services.AddSingleton<IQylStore>(CreateStore);
        services.ActivateSingleton<IQylStore>();
        services.AddHealthChecks()
            .AddCheck<DuckDbHealthCheck>("duckdb", tags: ["db", "storage", QylEndpoints.ReadyTag]);

        return services;
    }

    private static IQylStore CreateStore(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var dataPath = config["QYL_DATA_PATH"] ?? DefaultDataPath;
        var dataDir = Path.GetDirectoryName(dataPath);
        if (!string.IsNullOrEmpty(dataDir))
            Directory.CreateDirectory(dataDir);

        return new DuckDbStore(
            dataPath,
            memoryLimit: config["QYL_DB_MEMORY_LIMIT"],
            threads: config["QYL_DB_THREADS"] is { } threads ? int.Parse(threads, CultureInfo.InvariantCulture) : null,
            tempDirectory: config["QYL_DB_TEMP_DIR"]);
    }
}
