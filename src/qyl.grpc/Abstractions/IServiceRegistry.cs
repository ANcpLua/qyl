namespace qyl.Grpc.Abstractions;

public interface IServiceRegistry
{
    void Register(ServiceIdentity service);
    void UpdateLastSeen(string serviceName);
    IReadOnlyList<ServiceIdentity> GetAll();
    ServiceIdentity? Get(string serviceName);
    ServiceStatistics GetStatistics(string serviceName);
}

public sealed record ServiceIdentity(
    string Name,
    string? Version,
    string? Namespace,
    string? Environment,
    string? SdkLanguage,
    string? SdkVersion,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen);

public sealed record ServiceStatistics(
    string ServiceName,
    long TraceCount,
    long SpanCount,
    long ErrorCount,
    long LogCount,
    double ErrorRate,
    DateTimeOffset? OldestData,
    DateTimeOffset? NewestData);
