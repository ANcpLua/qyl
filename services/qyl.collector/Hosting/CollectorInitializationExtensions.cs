using Qyl.Collector.Cost;
using Qyl.Collector.Telemetry;

namespace Qyl.Collector.Hosting;

internal static class CollectorInitializationExtensions
{
    public static async Task InitializeQylCollectorAsync(this WebApplication app)
    {
        var duckDbStore = app.Services.GetRequiredService<DuckDbStore>();
        QylMetrics.RegisterStorageSizeCallback(duckDbStore.GetStorageSizeBytes);

        await app.Services.GetRequiredService<ModelPricingService>().InitializeAsync().ConfigureAwait(false);
    }
}
