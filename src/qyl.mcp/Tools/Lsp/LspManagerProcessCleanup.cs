// Copyright (c) 2025-2026 ancplua

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace qyl.mcp.Tools.Lsp;

/// <summary>
///     Disposes the <see cref="LspClientWrapper" />'s client pool on host shutdown so orphaned
///     LSP processes don't leak. Start is a no-op — clients are started lazily by
///     <see cref="LspClientWrapper.OpenAsync" />.
/// </summary>
internal sealed class LspManagerProcessCleanup(
    LspClientWrapper wrapper,
    ILogger<LspManagerProcessCleanup> logger) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Shutting down LSP client pool");
        await wrapper.DisposeAsync().ConfigureAwait(false);
    }
}
