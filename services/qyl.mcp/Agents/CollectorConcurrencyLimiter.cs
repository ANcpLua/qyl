using Microsoft.Extensions.Configuration;

namespace qyl.mcp.Agents;

/// <summary>
///     HTTP delegating handler that limits concurrent requests to the qyl collector.
///     DuckDB is single-writer; unbounded concurrency from autonomous agents saturates it.
///     Excess requests queue on the semaphore. If queued longer than <see cref="s_queueTimeout" />,
///     the request proceeds anyway to avoid deadlock.
/// </summary>
internal sealed class CollectorConcurrencyLimiter : DelegatingHandler, IDisposable
{
    private const int DefaultMaxConcurrency = 5;
    private static readonly TimeSpan s_queueTimeout = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _semaphore;

    public CollectorConcurrencyLimiter(IConfiguration configuration)
    {
        var maxConcurrency = configuration.GetValue<int?>("QYL_COLLECTOR_MAX_CONCURRENT") ?? DefaultMaxConcurrency;

        if (maxConcurrency <= 0)
        {
            maxConcurrency = DefaultMaxConcurrency;
        }

        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public new void Dispose()
    {
        _semaphore.Dispose();
        base.Dispose();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var acquired = await _semaphore.WaitAsync(s_queueTimeout, cancellationToken);

        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            if (acquired)
            {
                _semaphore.Release();
            }
        }
    }
}
