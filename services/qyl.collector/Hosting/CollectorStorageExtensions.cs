using Microsoft.Extensions.Options;
using Qyl.Collector.Health;
using Qyl.Collector.Retention;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Hosting;

internal static class CollectorStorageExtensions
{

    public static IServiceCollection AddQylCollectorStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RetentionOptions>()
            .Bind(configuration)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<RetentionOptions>, RetentionOptionsValidator>();
        services.AddSingleton(CreateStore);
        services.ActivateSingleton<IQylStore>();
        services.AddHostedService<RetentionService>();
        services.AddHealthChecks()
            .AddCheck<DuckDbHealthCheck>("duckdb", tags: ["db", "storage", QylEndpoints.ReadyTag]);

        return services;
    }

    private static IQylStore CreateStore(IServiceProvider services)
    {
        var storage = services.GetRequiredService<IOptions<CollectorStorageOptions>>().Value;
        var dataDir = Path.GetDirectoryName(storage.DataPath);
        if (!string.IsNullOrEmpty(dataDir))
            Directory.CreateDirectory(dataDir);

        return new DuckDbStore(
            storage.DataPath,
            memoryLimit: storage.MemoryLimit,
            threads: storage.Threads,
            tempDirectory: storage.TempDirectory,
            loggerFactory: services.GetRequiredService<ILoggerFactory>());
    }
}
