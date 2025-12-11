using System.Collections.Concurrent;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using qyl.grpc.Abstractions;

namespace qyl.grpc.Streaming;

public static class SseExtensions
{
    public static IServiceCollection AddTelemetrySse(this IServiceCollection services)
    {
        services.AddSingleton<ITelemetrySseBroadcaster, TelemetrySseBroadcaster>();
        return services;
    }

    public static IEndpointRouteBuilder MapTelemetrySse(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/live", HandleLiveStream)
            .WithName("TelemetryLiveStream")
            .WithTags("Streaming");

        endpoints.MapGet("/api/v1/live/spans", (ITelemetrySseBroadcaster stream, HttpContext ctx) =>
                HandleFilteredStream(stream, ctx, TelemetrySignal.Trace, "spans"))
            .WithName("SpansLiveStream");

        endpoints.MapGet("/api/v1/live/metrics", (ITelemetrySseBroadcaster stream, HttpContext ctx) =>
                HandleFilteredStream(stream, ctx, TelemetrySignal.Metric, "metrics"))
            .WithName("MetricsLiveStream");

        endpoints.MapGet("/api/v1/live/logs", (ITelemetrySseBroadcaster stream, HttpContext ctx) =>
                HandleFilteredStream(stream, ctx, TelemetrySignal.Log, "logs"))
            .WithName("LogsLiveStream");

        return endpoints;
    }

    private static IResult HandleLiveStream(ITelemetrySseBroadcaster stream, HttpContext context)
    {
        var clientId = Guid.NewGuid();
        var reader = stream.Subscribe(clientId);
        context.RequestAborted.Register(() => stream.Unsubscribe(clientId));

        return TypedResults.ServerSentEvents(StreamEventsAsync(reader, context.RequestAborted));
    }

    private static IResult HandleFilteredStream(
        ITelemetrySseBroadcaster stream,
        HttpContext context,
        TelemetrySignal filter,
        string eventType)
    {
        var clientId = Guid.NewGuid();
        var reader = stream.Subscribe(clientId);
        context.RequestAborted.Register(() => stream.Unsubscribe(clientId));

        return TypedResults.ServerSentEvents(
            StreamFilteredAsync(reader, filter, context.RequestAborted),
            eventType);
    }

    private static async IAsyncEnumerable<SseItem<TelemetrySseEvent>> StreamEventsAsync(
        ChannelReader<TelemetryMessage> reader,
        [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new(
            new("connected", null, DateTimeOffset.UtcNow),
            "connected");

        await foreach (var message in reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            var eventType = message.Signal switch
            {
                TelemetrySignal.Trace => "spans",
                TelemetrySignal.Metric => "metrics",
                TelemetrySignal.Log => "logs",
                _ => "data"
            };

            yield return new(
                new(eventType, message.Data, message.Timestamp),
                eventType);
        }
    }

    private static async IAsyncEnumerable<object?> StreamFilteredAsync(
        ChannelReader<TelemetryMessage> reader,
        TelemetrySignal filter,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var message in reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            if (message.Signal == filter)
                yield return message.Data;
        }
    }
}

public sealed record TelemetrySseEvent(
    string EventType,
    object? Data,
    DateTimeOffset Timestamp);

public interface ITelemetrySseBroadcaster : IAsyncDisposable
{
    int ClientCount { get; }
    ChannelReader<TelemetryMessage> Subscribe(Guid clientId);
    void Unsubscribe(Guid clientId);
    void Publish(TelemetryMessage message);
}

public sealed class TelemetrySseBroadcaster : ITelemetrySseBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<TelemetryMessage>> _channels = new();
    private volatile bool _disposed;

    public int ClientCount => _channels.Count;

    public ChannelReader<TelemetryMessage> Subscribe(Guid clientId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = Channel.CreateBounded<TelemetryMessage>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = true
        });

        _channels[clientId] = channel;
        return channel.Reader;
    }

    public void Unsubscribe(Guid clientId)
    {
        if (_channels.TryRemove(clientId, out var channel))
            channel.Writer.TryComplete();
    }

    public void Publish(TelemetryMessage message)
    {
        if (_disposed) return;

        foreach (var channel in _channels.Values)
            channel.Writer.TryWrite(message);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        foreach (var channel in _channels.Values)
            channel.Writer.TryComplete();

        _channels.Clear();
        return ValueTask.CompletedTask;
    }
}
