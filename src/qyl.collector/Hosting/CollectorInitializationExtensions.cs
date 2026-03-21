using Qyl.Collector.Identity;
using Qyl.Collector.Telemetry;

namespace Qyl.Collector.Hosting;

public static class CollectorInitializationExtensions
{
    public static async Task InitializeQylCollectorAsync(this WebApplication app)
    {
        var duckDbStore = app.Services.GetRequiredService<DuckDbStore>();
        QylMetrics.RegisterStorageSizeCallback(duckDbStore.GetStorageSizeBytes);

        var migrationRunner = app.Services.GetRequiredService<MigrationRunner>();
        var migrationDirectory = Path.Combine(app.Environment.ContentRootPath, "Storage", "Migrations");
        const int collectorSchemaVersion = 20260214;
        migrationRunner.ApplyPendingMigrations(duckDbStore.Connection, collectorSchemaVersion, migrationDirectory);

        await app.Services.GetRequiredService<GitHubService>().InitializeAsync().ConfigureAwait(false);
    }
}
