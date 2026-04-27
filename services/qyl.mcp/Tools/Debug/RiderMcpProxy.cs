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

    public string GetStatus() =>
        _client is not null ? $"Connected to {_connectedUrl}" : "Not connected";

    private async Task<McpClient> GetOrConnectAsync(CancellationToken ct)
    {
        var endpoints = discovery.GetEndpoints();
        var url = endpoints?.DebuggerStreamableUrl
                  ?? throw new InvalidOperationException(
                      "Rider debugger MCP not found. Is Rider running with the Debugger MCP plugin?");

        // Reconnect if URL changed (Rider restarted)
        if (_client is not null && _connectedUrl == url)
            return _client;

        await DisposeClientAsync().ConfigureAwait(false);

        LogConnecting(url);

        _transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(url), TransportMode = HttpTransportMode.StreamableHttp, Name = "rider-debugger"
        });

        _client = await McpClient.CreateAsync(_transport, cancellationToken: ct).ConfigureAwait(false);
        _connectedUrl = url;

        LogConnected(_client.ServerInfo.Name);

        return _client;
    }

    private async Task DisposeClientAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync().ConfigureAwait(false);
        if (_transport is not null)
            await _transport.DisposeAsync().ConfigureAwait(false);
        _client = null;
        _transport = null;
        _connectedUrl = null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to Rider debugger MCP at {Url}")]
    private partial void LogConnecting(string url);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to Rider debugger MCP — server: {ServerName}")]
    private partial void LogConnected(string serverName);
}
