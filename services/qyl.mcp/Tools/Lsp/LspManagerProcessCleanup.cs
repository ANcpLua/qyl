
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace qyl.mcp.Tools.Lsp;

internal sealed partial class LspManagerProcessCleanup(
    LspClientWrapper wrapper,
    ILogger<LspManagerProcessCleanup> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        LogShuttingDownClientPool(logger);
        await wrapper.DisposeAsync().ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Shutting down LSP client pool")]
    private static partial void LogShuttingDownClientPool(ILogger logger);
}
