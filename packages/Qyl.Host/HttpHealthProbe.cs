namespace Qyl.Host;

/// <summary>
/// The default <see cref="IReadinessProbe"/>: GET the resource's health path until it answers
/// 2xx or the startup deadline passes. Each attempt is bounded by the named health-probe
/// client's own timeout; this loop is the retry policy.
/// </summary>
internal sealed class HttpHealthProbe(
    IHttpClientFactory httpClientFactory,
    string healthPath,
    TimeSpan startupTimeout,
    TimeProvider time) : IReadinessProbe
{
    public async Task<bool> IsReadyAsync(QylResourceState state, CancellationToken cancellationToken)
    {
        if (state.Endpoint is not { } baseEndpoint) return false;

        using var client = httpClientFactory.CreateClient(QylConstants.HttpClients.HealthProbe);
        var deadline = time.GetUtcNow().Add(startupTimeout);
        var probeUri = new Uri(baseEndpoint, healthPath);

        while (!cancellationToken.IsCancellationRequested && time.GetUtcNow() < deadline)
        {
            try
            {
                using var response = await client.GetAsync(probeUri, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode) return true;
            }
            catch (HttpRequestException)
            {
                // Service not yet listening — keep polling until cancellationToken fires.
            }
            catch (TaskCanceledException)
            {
                // Probe timeout or shutdown — let the next loop iteration handle it.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(QylConstants.Orchestrator.HealthPollIntervalMs), time,
                cancellationToken).ConfigureAwait(false);
        }

        return false;
    }
}
