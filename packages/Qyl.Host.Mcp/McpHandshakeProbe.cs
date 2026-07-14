using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Qyl.Host;

namespace Qyl.Host.Mcp;

/// <summary>
/// Readiness requires an initialized client that answers <c>tools/list</c>; HTTP
/// reachability is insufficient. Successful connections are registered in
/// <see cref="McpClientRegistry"/> for passthrough.
/// </summary>
internal sealed class McpHandshakeProbe(
    Func<QylResourceState, CancellationToken, Task<McpConnection>> connect,
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
            McpConnection? connection = null;
            try
            {
                connection = await connect(state, cancellationToken).ConfigureAwait(false);
                _ = await connection.Client.ListToolsAsync(new ListToolsRequestParams(), cancellationToken)
                    .ConfigureAwait(false);

                registry.Register(state.Name, connection);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception)
            {
                // Server not up yet (connect refused, handshake failed, tools/list errored) — dispose
                // the whole attempt (client plus any in-process server) before retrying.
                if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false);
            }

            await Task.Delay(s_retryInterval, time, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }
}
