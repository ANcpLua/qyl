using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using qyl.collector.Storage;

namespace qyl.collector.Realtime;

public static class SseEndpoints
{
    public static IEndpointRouteBuilder MapSseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/live", HandleLiveStream);

        endpoints.MapGet("/api/v1/live/spans", (ITelemetrySseBroadcaster stream, HttpContext ctx) =>
            HandleFilteredStream(stream, ctx, TelemetrySignal.Spans));

        return endpoints;
    }

    private static IResult HandleLiveStream(ITelemetrySseBroadcaster stream, HttpContext context)
    {
        var clientId = Guid.NewGuid();
        var reader = stream.Subscribe(clientId);
        var sessionFilter = context.Request.Query["session"].FirstOrDefault();

        context.RequestAborted.Register(() => stream.Unsubscribe(clientId));

        return TypedResults.ServerSentEvents(
            StreamEventsAsync(reader, sessionFilter, context.RequestAborted),
            null
        );
    }

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
            filter.ToString().ToLowerInvariant()
        );
    }

    private static async IAsyncEnumerable<SseItem<TelemetryEventDto>> StreamEventsAsync(
        ChannelReader<TelemetryMessage> reader,
        string? sessionFilter,
        [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new SseItem<TelemetryEventDto>(
            new TelemetryEventDto("connected", new
                {
                    connectionId = Guid.NewGuid().ToString("N")[..8]
                },
                TimeProvider.System.GetUtcNow()),
            "connected"
        );

        await foreach (var message in reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            var messageToSend = message;
            if (sessionFilter is not null && message.Data is SpanBatch batch)
            {
                var filteredSpans = batch.Spans
                    .Where(s => string.Equals(s.SessionId, sessionFilter, StringComparison.Ordinal))
                    .ToList();

                if (filteredSpans.Count == 0) continue;
                messageToSend = message with
                {
                    Data = new SpanBatch(filteredSpans)
                };
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

            if (sessionFilter is not null && message.Data is SpanBatch batch)
            {
                var filteredSpans = batch.Spans
                    .Where(s => string.Equals(s.SessionId, sessionFilter, StringComparison.Ordinal))
                    .ToList();

                if (filteredSpans.Count == 0) continue;

                yield return new SseItem<object?>(new SpanBatch(filteredSpans), filter.ToString().ToLowerInvariant());
            }
            else
                yield return new SseItem<object?>(message.Data, filter.ToString().ToLowerInvariant());
        }
    }
}
