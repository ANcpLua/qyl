namespace Qyl.Collector.Observe;

internal sealed class ObservationSubscription : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly IDisposable _pipeline;
    private int _disposed;

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

    public string Id { get; }
    public string Filter { get; }
    public string Endpoint { get; }
    public DateTimeOffset CreatedAt { get; }
    public string? ContractHash { get; }
    public string? SchemaVersion { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
            return;

        _listener.Dispose();
        _pipeline.Dispose();
    }
}
