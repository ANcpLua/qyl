using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Qyl.Run;

namespace Qyl.Host.Mcp;

/// <summary>
/// MCP readiness: a server is ready when it completes the <c>initialize</c> handshake
/// (performed by <see cref="McpClient.CreateAsync"/>) AND answers <c>tools/list</c> — the
/// same two-step gate qyl.mcp's TS orchestrator applies before publishing Ready. On success
/// the connected client is parked in the <see cref="McpClientRegistry"/> for the passthrough;
/// an HTTP route appearing is meaningless for an MCP server, hence no HTTP health probe.
/// </summary>
public sealed class McpHandshakeProbe(
    Func<QylResourceState, CancellationToken, Task<McpClient>> connect,
    McpClientRegistry registry,
    TimeSpan startupTimeout,
    TimeProvider time) : IReadinessProbe
{
    private static readonly TimeSpan s_retryInterval = TimeSpan.FromMilliseconds(500);

    public async Task<bool> IsReadyAsync(QylResourceState state, CancellationToken cancellationToken)
    {
        var deadline = time.GetUtcNow().Add(startupTimeout);

        while (!cancellationToken.IsCancellationRequested && time.GetUtcNow() < deadline)
        {
            McpClient? client = null;
            try
            {
                client = await connect(state, cancellationToken).ConfigureAwait(false);
                _ = await client.ListToolsAsync(new ListToolsRequestParams(), cancellationToken)
                    .ConfigureAwait(false);

                registry.Register(state.Name, client);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception)
            {
                // Server not up yet (connect refused, handshake failed, tools/list errored) — dispose
                // any half-open client and retry until the deadline.
                if (client is not null) await client.DisposeAsync().ConfigureAwait(false);
            }

            await Task.Delay(s_retryInterval, time, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }
}
