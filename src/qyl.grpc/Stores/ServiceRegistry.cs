using System.Collections.Concurrent;
using qyl.grpc.Abstractions;
using qyl.grpc.Models;

namespace qyl.grpc.Stores;

public sealed class ServiceRegistry(
    ITelemetryStore<SpanModel> spanStore,
    ITelemetryStore<LogModel> logStore) : IServiceRegistry
{
    private readonly ConcurrentDictionary<string, ServiceEntry> _services = new();

    public void Register(ServiceIdentity service) =>
        _services.AddOrUpdate(
            service.Name,
            _ => new(service),
            (_, _) => new(service with
            {
                LastSeen = DateTimeOffset.UtcNow
            }));

    public void UpdateLastSeen(string serviceName)
    {
        if (_services.TryGetValue(serviceName, out ServiceEntry? entry))
        {
            _services.TryUpdate(
                serviceName,
                new(entry.Identity with
                {
                    LastSeen = DateTimeOffset.UtcNow
                }),
                entry);
        }
    }

    public IReadOnlyList<ServiceIdentity> GetAll() =>
    [
        .. _services.Values
            .Select(e => e.Identity)
            .OrderBy(s => s.Name)
    ];

    public ServiceIdentity? Get(string serviceName) =>
        _services.TryGetValue(serviceName, out ServiceEntry? entry) ? entry.Identity : null;

    public ServiceStatistics GetStatistics(string serviceName)
    {
        var query = new TelemetryQuery(serviceName, Limit: int.MaxValue);
        IReadOnlyList<SpanModel> spans = spanStore.Query(query);
        IReadOnlyList<LogModel> logs = logStore.Query(query);

        int traceIds = spans.Select(s => s.TraceId).Distinct().Count();
        int errorCount = spans.Count(s => s.Status == SpanStatus.Error);
        double errorRate = spans.Count > 0 ? (double)errorCount / spans.Count : 0;

        var allTimestamps = spans.Select(s => s.StartTime)
            .Concat(logs.Select(l => l.Timestamp))
            .ToList();

        return new(
            serviceName,
            traceIds,
            spans.Count,
            errorCount,
            logs.Count,
            errorRate,
            allTimestamps.Count > 0 ? allTimestamps.Min() : null,
            allTimestamps.Count > 0 ? allTimestamps.Max() : null);
    }

    public void RegisterFromResource(ResourceModel resource)
    {
        var identity = new ServiceIdentity(
            resource.ServiceName,
            resource.ServiceVersion,
            resource.ServiceNamespace,
            resource.DeploymentEnvironment,
            resource.SdkLanguage,
            resource.SdkVersion,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        Register(identity);
    }

    private sealed record ServiceEntry(ServiceIdentity Identity);
}
