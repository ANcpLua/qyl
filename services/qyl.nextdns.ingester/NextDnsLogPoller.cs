using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Qyl.NextDns.Ingester;

/// <summary>
/// Hosted service that polls the NextDNS logs API in a cancellation-aware loop.
/// On 429 or 5xx responses it backs off exponentially up to one hour. Cursor
/// state is in-memory: a restart re-reads from the API's natural head, NextDNS
/// returns recent rows first so duplicate suppression on the collector side
/// (ON CONFLICT on entry_id) is enough.
/// </summary>
internal sealed partial class NextDnsLogPoller(
    NextDnsClient client,
    IngesterOptions options,
    TimeProvider timeProvider,
    ILogger<NextDnsLogPoller> logger) : BackgroundService
{
    private static readonly TimeSpan s_minBackoff = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan s_maxBackoff = TimeSpan.FromMinutes(60);

    private string? _cursor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(options.ProfileId, options.PollInterval, options.DryRun);

        var backoff = s_minBackoff;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var page = await client.FetchLogsAsync(_cursor, stoppingToken).ConfigureAwait(false);
                var entries = page?.Data ?? [];

                foreach (var entry in entries)
                {
                    if (options.DryRun)
                        LogDryDecision(entry.Domain, entry.Status);
                    else
                        IngesterTelemetry.RecordDecision(entry);
                }

                if (entries.Count > 0)
                    LogPolled(entries.Count);

                _cursor = page?.Meta?.Pagination?.Cursor ?? _cursor;
                backoff = s_minBackoff;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogPollFailed(ex, backoff);
                await DelayAsync(backoff, stoppingToken).ConfigureAwait(false);
                backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, s_maxBackoff.Ticks));
                continue;
            }

            await DelayAsync(options.PollInterval, stoppingToken).ConfigureAwait(false);
        }

        LogStopped();
    }

    private async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopping — swallowed so the loop exits cleanly.
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "NextDNS ingester started — profile={ProfileId} interval={Interval} dryRun={DryRun}")]
    private partial void LogStarted(string profileId, TimeSpan interval, bool dryRun);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "DRY: nextdns decision domain={Domain} status={Status}")]
    private partial void LogDryDecision(string? domain, string? status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Polled {Count} NextDNS log rows.")]
    private partial void LogPolled(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "NextDNS poll failed — backing off for {Backoff}.")]
    private partial void LogPollFailed(Exception ex, TimeSpan backoff);

    [LoggerMessage(Level = LogLevel.Information, Message = "NextDNS ingester stopped.")]
    private partial void LogStopped();
}
