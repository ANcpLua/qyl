namespace Qyl.Orchestration.Internal;

internal sealed class HttpHealthProbe(
    IHttpClientFactory httpClientFactory,
    string healthPath,
    TimeSpan startupTimeout,
    TimeProvider time)
{
    public async Task<bool> IsReadyAsync(QylResourceState state, CancellationToken cancellationToken,
        Process? process = null, bool requireProcessId = false)
    {
        if (state.Endpoint is not { } baseEndpoint) return false;

        using var client = httpClientFactory.CreateClient(OrchestrationDefaults.HttpClients.HealthProbe);
        using var deadline = new CancellationTokenSource(startupTimeout, time);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var probeToken = lifetime.Token;
        var probeUri = new Uri(baseEndpoint, healthPath);

        try
        {
            while (true)
            {
                probeToken.ThrowIfCancellationRequested();
                if (process?.HasExited == true) return false;
                try
                {
                    using var response = await client.GetAsync(probeUri, probeToken).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode && (!requireProcessId || process is not null &&
                        await response.Content.ReadAsStringAsync(probeToken).ConfigureAwait(false)
                            == process.Id.ToString(CultureInfo.InvariantCulture)))
                    {
                        probeToken.ThrowIfCancellationRequested();
                        return process?.HasExited != true;
                    }
                }
                catch (HttpRequestException)
                {
                    // Service not yet listening — keep polling within the startup budget.
                }
                catch (OperationCanceledException) when (!probeToken.IsCancellationRequested)
                {
                    // The client's per-attempt timeout elapsed; the startup budget still applies.
                }

                var interval = TimeSpan.FromMilliseconds(OrchestrationDefaults.Orchestrator.HealthPollIntervalMs);
                await Task.Delay(interval, time, probeToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // An owned deadline means not ready; caller cancellation remains observable.
        }

        return false;
    }
}
