
using System.Collections.ObjectModel;

namespace Qyl.Run;

public sealed record QylResource
{
    public required string Name { get; init; }

    public required string Kind { get; init; }

    public required int Port { get; init; }

    // Collector resources only: the OTLP receiver ports the child is pinned to via
    // QYL_OTLP_PORT / QYL_GRPC_PORT. 0 = not an OTLP receiver.
    public int OtlpHttpPort { get; init; }

    public int GrpcPort { get; init; }

    // Names of resources that must report Ready before this resource's process is launched.
    public IReadOnlyList<string> WaitsFor { get; init; } = [];

    public required QylLaunchSpec Launch { get; init; }
}

public sealed record QylLaunchSpec
{
    public required string Executable { get; init; }
    public ReadOnlyCollection<string> Args { get; init; } = ReadOnlyCollection<string>.Empty;
    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>();
    public string? WorkingDirectory { get; init; }
    public string HealthPath { get; init; } = QylConstants.Routes.Health;
}

public enum ResourceLifecycle
{
    Pending,
    Starting,
    Ready,
    Stopping,
    Stopped,
    Failed
}

public sealed record QylResourceState
{
    public required string Name { get; init; }
    public required ResourceLifecycle Lifecycle { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public int? AllocatedPort { get; init; }
    public Uri? Endpoint { get; init; }
    public string? LastError { get; init; }
}
