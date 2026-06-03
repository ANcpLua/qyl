namespace Qyl.Collector.Hosting;

internal static class CollectorStorageExtensions
{
    private const string DefaultDataPath = "qyl.duckdb";

    public static IServiceCollection AddQylCollectorStorage(this IServiceCollection services)
    {
        services.AddSingleton<IQylStore>(CreateStore);
        services.ActivateSingleton<IQylStore>();

        return services;
    }

    private static IQylStore CreateStore(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var dataPath = config["QYL_DATA_PATH"] ?? DefaultDataPath;
        var dataDir = Path.GetDirectoryName(dataPath);
        if (!string.IsNullOrEmpty(dataDir))
            Directory.CreateDirectory(dataDir);

        return new DuckDbStore(dataPath);
    }
}
