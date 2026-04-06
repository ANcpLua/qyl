using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace qyl.mcp.Tools.Debug;

/// <summary>
///     Lazily connects to Rider's debugger MCP server and proxies tool calls.
///     Reconnects automatically when Rider restarts (URL changes).
/// </summary>
internal sealed partial class RiderMcpProxy(
    JetBrainsDiscovery discovery,
    ILogger<RiderMcpProxy> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private McpClient? _client;
    private string? _connectedUrl;
    private HttpClientTransport? _transport;

    public async ValueTask DisposeAsync() => await DisposeClientAsync().ConfigureAwait(false);

    public async Task<CallToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken ct = default)
    {
        var client = await GetOrConnectAsync(ct).ConfigureAwait(false);
        return await client.CallToolAsync(toolName, arguments, cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct = default)
    {
        var client = await GetOrConnectAsync(ct).ConfigureAwait(false);
        return await client.ListToolsAsync(cancellationToken: ct).ConfigureAwait(false);
    }

    public string? GetStatus() =>
        _client is not null ? $"Connected to {_connectedUrl}" : "Not connected";

    private async Task<McpClient> GetOrConnectAsync(CancellationToken ct)
    {
        var endpoints = discovery.GetEndpoints();
        var url = endpoints?.DebuggerStreamableUrl
                  ?? throw new InvalidOperationException(
                      "Rider debugger MCP not found. Is Rider running with the Debugger MCP plugin?");

        // Fast path: already connected to this URL (volatile read via Interlocked not needed —
        // the semaphore provides the memory barrier for writers, and a stale read here
        // just falls through to the serialized slow path)
        if (_client is not null && _connectedUrl == url)
            return _client;

        // Slow path: serialize connect/reconnect so only one caller initializes
        await _connectGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring the gate
            if (_client is not null && _connectedUrl == url)
                return _client;

            await DisposeClientAsync().ConfigureAwait(false);

            LogConnecting(url);

            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(url), TransportMode = HttpTransportMode.StreamableHttp, Name = "rider-debugger"
            });

            var client = await McpClient.CreateAsync(transport, cancellationToken: ct).ConfigureAwait(false);

            _transport = transport;
            _client = client;
            _connectedUrl = url;

            LogConnected(client.ServerInfo?.Name ?? "unknown");

            return client;
        }
        finally
        {
            _connectGate.Release();
        }
    }

    private async Task DisposeClientAsync()
    {
        var client = _client;
        var transport = _transport;
        _client = null;
        _transport = null;
        _connectedUrl = null;

        try
        {
            if (client is not null)
                await client.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            if (transport is not null)
                await transport.DisposeAsync().ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to Rider debugger MCP at {Url}")]
    private partial void LogConnecting(string url);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to Rider debugger MCP — server: {ServerName}")]
    private partial void LogConnected(string serverName);
}
