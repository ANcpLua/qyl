using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Qyl.Instrumentation.Discovery;

internal sealed class CollectorDiscoveryLogger(ILogger<CollectorDiscoveryLogger> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        CollectorDiscovery.LogResult(logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
