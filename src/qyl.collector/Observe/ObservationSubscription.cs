using System.Diagnostics;

namespace qyl.collector.Observe;

/// <summary>
/// An active observability subscription: a named filter wired to a live OTLP export pipeline.
/// Disposing tears down the ActivityListener (HasListeners() flips back to false) then drains
/// and disposes the export pipeline, returning the monitored ActivitySource to zero-cost dormancy.
/// </summary>
internal sealed class ObservationSubscription : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly IDisposable _pipeline;
    private int _disposed;

    public string Id { get; }
    public string Filter { get; }
    public string Endpoint { get; }
    public DateTimeOffset CreatedAt { get; }
    public string? ContractHash { get; }
    public string? SchemaVersion { get; }

    internal ObservationSubscription(
        string id,
        string filter,
        string endpoint,
        ActivityListener listener,
        IDisposable pipeline,
        string? contractHash = null,
        string? schemaVersion = null)
    {
        Id = id;
        Filter = filter;
        Endpoint = endpoint;
        ContractHash = contractHash;
        SchemaVersion = schemaVersion;
        CreatedAt = TimeProvider.System.GetUtcNow();
        _listener = listener;
        _pipeline = pipeline;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        // Order matters:
        // 1. Stop the listener first — HasListeners() flips to false, no new spans enter the pipeline
        // 2. Dispose the pipeline — drains in-flight spans and flushes to the exporter
        _listener.Dispose();
        _pipeline.Dispose();
    }
}
