using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Qyl.ServiceDefaults.Discovery;

/// <summary>
///     Logs the collector discovery result once at application startup.
/// </summary>
internal sealed partial class CollectorDiscoveryLogger(ILogger<CollectorDiscoveryLogger> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        CollectorDiscovery.LogResult(logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
