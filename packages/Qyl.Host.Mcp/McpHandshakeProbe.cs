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
                // Server not up yet (connect refused, handshake failed, tools/list errored).
                if (client is not null)
                {
                    try
                    {
                        await client.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is IOException or InvalidOperationException)
                    {
                        // The failed peer may already have closed its transport.
                    }
                }
            }

            await Task.Delay(s_retryInterval, time, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }
}
