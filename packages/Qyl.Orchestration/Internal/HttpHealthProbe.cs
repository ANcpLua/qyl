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
        var deadline = time.GetUtcNow().Add(startupTimeout);
        var probeUri = new Uri(baseEndpoint, healthPath);

        while (!cancellationToken.IsCancellationRequested && time.GetUtcNow() < deadline)
        {
            if (process?.HasExited == true) return false;
            try
            {
                using var response = await client.GetAsync(probeUri, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode && (!requireProcessId || process is not null &&
                    await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
                        == process.Id.ToString(CultureInfo.InvariantCulture)))
                    return process?.HasExited != true;
            }
            catch (HttpRequestException)
            {
                // Service not yet listening — keep polling until cancellationToken fires.
            }
            catch (TaskCanceledException)
            {
                // Probe timeout or shutdown — let the next loop iteration handle it.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(OrchestrationDefaults.Orchestrator.HealthPollIntervalMs), time,
                cancellationToken).ConfigureAwait(false);
        }

        return false;
    }
}
