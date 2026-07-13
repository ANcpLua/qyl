using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Qyl.Host.Mcp;

/// <summary>
/// The live MCP clients, keyed by resource name. <see cref="McpHandshakeProbe"/> parks a client
/// here the moment its handshake succeeds; the <c>/runner/mcp</c> passthrough is the consumer.
/// A name with no entry is either unknown or not yet Ready — the passthrough distinguishes the
/// two against the composition's resource list.
/// </summary>
internal sealed class McpClientRegistry : IHostedService, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, McpConnection> _connections = new(StringComparer.Ordinal);
    private int _disposed;

    public void Register(string name, McpConnection connection)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) is not 0, this);
        if (!_connections.TryAdd(name, connection))
            throw new InvalidOperationException($"An MCP connection named '{name}' is already registered.");
    }

    public bool TryGet(string name, [NotNullWhen(true)] out McpClient? client) =>
        TryGetClient(name, out client);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => DisposeAsync().AsTask();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0) return;

        foreach (var (name, connection) in _connections)
        {
            if (_connections.TryRemove(name, out _))
                await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private bool TryGetClient(string name, [NotNullWhen(true)] out McpClient? client)
    {
        if (_connections.TryGetValue(name, out var connection))
        {
            client = connection.Client;
            return true;
        }

        client = null;
        return false;
    }
}

internal sealed class McpConnection : IAsyncDisposable
{
    private readonly McpServer? _server;
    private readonly Task? _serverLoop;
    private readonly CancellationTokenSource? _serverLifetime;
    private readonly ITransport? _serverTransport;
    private int _disposed;

    internal McpConnection(McpClient client)
    {
        Client = client;
    }

    internal McpConnection(
        McpClient client,
        McpServer server,
        Task serverLoop,
        CancellationTokenSource serverLifetime,
        ITransport serverTransport)
    {
        Client = client;
        _server = server;
        _serverLoop = serverLoop;
        _serverLifetime = serverLifetime;
        _serverTransport = serverTransport;
    }

    internal McpClient Client { get; }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0) return;

        try
        {
            await Client.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // The peer may already have closed during host shutdown.
        }

        _serverLifetime?.Cancel();
        if (_serverLoop is not null)
        {
            try
            {
                await _serverLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when the host stops an in-process server.
            }
            catch (IOException)
            {
                // The client closing its pipe can terminate the server loop first.
            }
        }

        if (_server is not null) await _server.DisposeAsync().ConfigureAwait(false);
        if (_serverTransport is not null) await _serverTransport.DisposeAsync().ConfigureAwait(false);
        _serverLifetime?.Dispose();
    }
}
