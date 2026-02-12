using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace qyl.watch;

/// <summary>
///     SSE client that connects to the collector's live telemetry stream.
///     Supports automatic reconnection with exponential backoff.
/// </summary>
internal sealed class SseClient(string baseUrl) : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private const int MaxBackoffSeconds = 30;

    public IAsyncEnumerable<SseEvent> StreamAsync(string? session, CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<SseEvent>(new UnboundedChannelOptions { SingleReader = true });

        // Fire-and-forget producer that writes events into the channel
        _ = ProduceWithReconnectAsync(session, channel.Writer, ct);

        return channel.Reader.ReadAllAsync(ct);
    }

    private async Task ProduceWithReconnectAsync(string? session, ChannelWriter<SseEvent> writer, CancellationToken ct)
    {
        var backoff = 1;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var url = $"{baseUrl}/api/v1/live";
                if (session is not null)
                    url += $"?session={Uri.EscapeDataString(session)}";

                try
                {
                    await ProduceEventsAsync(url, writer, ct).ConfigureAwait(false);
                    backoff = 1;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch
                {
                    // Connection lost — will retry
                }

                if (ct.IsCancellationRequested)
                    return;

                // Exponential backoff before reconnect
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(backoff), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                backoff = Math.Min(backoff * 2, MaxBackoffSeconds);
            }
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task ProduceEventsAsync(string url, ChannelWriter<SseEvent> writer, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new("text/event-stream"));

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        string? eventType = null;
        var dataLines = new List<string>();

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);

            if (line is null)
                return; // Stream closed

            if (line.Length == 0)
            {
                if (dataLines.Count > 0)
                {
                    var data = string.Join('\n', dataLines);
                    await writer.WriteAsync(new SseEvent(eventType ?? "message", data), ct).ConfigureAwait(false);
                    eventType = null;
                    dataLines.Clear();
                }
                continue;
            }

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                eventType = line[6..].Trim();
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                dataLines.Add(line[5..].TrimStart());
            }
        }
    }

    public void Dispose() => _http.Dispose();
}
