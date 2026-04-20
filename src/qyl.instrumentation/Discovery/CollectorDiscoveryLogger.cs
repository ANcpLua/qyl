namespace Qyl.Instrumentation.Discovery;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
///     Logs the collector discovery result once at application startup.
/// </summary>
internal sealed class CollectorDiscoveryLogger(ILogger<CollectorDiscoveryLogger> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        CollectorDiscovery.LogResult(logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
