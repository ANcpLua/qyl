using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using ModelContextProtocol.Client;

namespace Qyl.Host.Mcp;

/// <summary>
/// The live MCP clients, keyed by resource name. <see cref="McpHandshakeProbe"/> parks a client
/// here the moment its handshake succeeds; the <c>/runner/mcp</c> passthrough is the consumer.
/// A name with no entry is either unknown or not yet Ready — the passthrough distinguishes the
/// two against the composition's resource list.
/// </summary>
public sealed class McpClientRegistry
{
    private readonly ConcurrentDictionary<string, McpClient> _clients = new(StringComparer.Ordinal);

    public void Register(string name, McpClient client) => _clients[name] = client;

    public bool TryGet(string name, [NotNullWhen(true)] out McpClient? client) =>
        _clients.TryGetValue(name, out client);
}
