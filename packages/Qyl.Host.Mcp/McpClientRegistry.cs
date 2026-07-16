using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;

namespace Qyl.Host.Mcp;

/// <summary>
/// The live MCP clients, keyed by resource name. <see cref="McpHandshakeProbe"/> parks a client
/// here the moment its handshake succeeds; the <c>/runner/mcp</c> passthrough is the consumer.
/// A name with no entry is either unknown or not yet Ready — the passthrough distinguishes the
/// two against the composition's resource list.
/// </summary>
internal sealed class McpClientRegistry : IHostedService, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, McpClient> _clients = new(StringComparer.Ordinal);
    private int _disposed;

    public void Register(string name, McpClient client)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) is not 0, this);
        if (!_clients.TryAdd(name, client))
            throw new InvalidOperationException($"An MCP connection named '{name}' is already registered.");
    }

    public bool TryGet(string name, [NotNullWhen(true)] out McpClient? client) =>
        _clients.TryGetValue(name, out client);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => DisposeAsync().AsTask();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0) return;

        foreach (var (name, client) in _clients)
        {
            if (!_clients.TryRemove(name, out _)) continue;

            try
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                // The peer may already have closed during host shutdown.
            }
        }
    }
}
