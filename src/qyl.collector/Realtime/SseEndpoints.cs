// qyl.collector - SSE Endpoints
// .NET 10 Native Server-Sent Events implementation

using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using qyl.collector.Storage;

namespace qyl.collector.Realtime;

/// <summary>
/// .NET 10 native SSE endpoints for real-time telemetry streaming.
/// </summary>
public static class SseEndpoints
{
    /// <summary>
    /// Maps SSE endpoints for telemetry streaming.
    /// </summary>
    public static IEndpointRouteBuilder MapSseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Main telemetry stream (all signals)
        endpoints.MapGet("/api/v1/live", HandleLiveStream);

        // Signal-specific streams (optional, for filtered subscriptions)
        endpoints.MapGet("/api/v1/live/spans", (ITelemetrySseBroadcaster stream, HttpContext ctx) =>
            HandleFilteredStream(stream, ctx, TelemetrySignal.Spans));

        return endpoints;
    }

    /// <summary>
    /// Main SSE endpoint - streams all telemetry signals using .NET 10 native API.
    /// </summary>
    private static IResult HandleLiveStream(ITelemetrySseBroadcaster stream, HttpContext context)
    {
        var clientId = Guid.NewGuid();
        var reader = stream.Subscribe(clientId);
        var sessionFilter = context.Request.Query["session"].FirstOrDefault();

        // Unsubscribe when client disconnects
        context.RequestAborted.Register(() => stream.Unsubscribe(clientId));

        return TypedResults.ServerSentEvents(
            StreamEventsAsync(reader, sessionFilter, context.RequestAborted),
            eventType: null  // Event type per-item
        );
    }

    /// <summary>
    /// Filtered SSE endpoint - streams only specific signal type.
    /// </summary>
    private static IResult HandleFilteredStream(
        ITelemetrySseBroadcaster stream,
        HttpContext context,
        TelemetrySignal filter)
    {
        var clientId = Guid.NewGuid();
        var reader = stream.Subscribe(clientId);
        var sessionFilter = context.Request.Query["session"].FirstOrDefault();
        context.RequestAborted.Register(() => stream.Unsubscribe(clientId));

        return TypedResults.ServerSentEvents(
            StreamFilteredEventsAsync(reader, filter, sessionFilter, context.RequestAborted),
            eventType: filter.ToString().ToLowerInvariant()
        );
    }

    private static async IAsyncEnumerable<SseItem<TelemetryEventDto>> StreamEventsAsync(
        ChannelReader<TelemetryMessage> reader,
        string? sessionFilter,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Send connection event first
        yield return new SseItem<TelemetryEventDto>(
            new TelemetryEventDto("connected", new { connectionId = Guid.NewGuid().ToString("N")[..8] }, DateTimeOffset.UtcNow),
            "connected"
        );

        // Simple streaming without heartbeats (TypedResults.ServerSentEvents handles connection)
        await foreach (var message in reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            // Apply session filter if set
            var messageToSend = message;
            if (sessionFilter is not null && message.Data is SpanBatch batch)
            {
                var filteredSpans = batch.Spans
                    .Where(s => string.Equals(s.SessionId, sessionFilter, StringComparison.Ordinal))
                    .ToList();

                if (filteredSpans.Count == 0) continue;
                messageToSend = message with { Data = new SpanBatch(filteredSpans) };
            }

            var eventType = messageToSend.Signal switch
            {
                TelemetrySignal.Spans => "spans",
                TelemetrySignal.Metrics => "metrics",
                TelemetrySignal.Logs => "logs",
                _ => "data"
            };

            yield return new SseItem<TelemetryEventDto>(
                new TelemetryEventDto(eventType, messageToSend.Data, messageToSend.Timestamp),
                eventType
            );
        }
    }

    private static async IAsyncEnumerable<SseItem<object?>> StreamFilteredEventsAsync(
        ChannelReader<TelemetryMessage> reader,
        TelemetrySignal filter,
        string? sessionFilter,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var message in reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            if (message.Signal != filter) continue;

            // Apply session filter if set
            if (sessionFilter is not null && message.Data is SpanBatch batch)
            {
                var filteredSpans = batch.Spans
                    .Where(s => string.Equals(s.SessionId, sessionFilter, StringComparison.Ordinal))
                    .ToList();

                if (filteredSpans.Count == 0) continue;

                yield return new SseItem<object?>(new SpanBatch(filteredSpans), filter.ToString().ToLowerInvariant());
            }
            else
            {
                yield return new SseItem<object?>(message.Data, filter.ToString().ToLowerInvariant());
            }
        }
    }
}
