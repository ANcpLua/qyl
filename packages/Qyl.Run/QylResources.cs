
using System.Collections.ObjectModel;

namespace Qyl.Run;

public sealed record QylResource
{
    public required string Name { get; init; }

    public required string Kind { get; init; }

    public required string Environment { get; init; }

    public required int Port { get; init; }

    public required QylLaunchSpec Launch { get; init; }

    public ReadOnlyCollection<string> WaitForNames { get; init; } = ReadOnlyCollection<string>.Empty;

    public string? Description { get; init; }

    // Non-null for container resources: the orchestrator drives an OCI runtime instead of the process launcher.
    public QylContainerSpec? Container { get; init; }
}

public sealed record QylLaunchSpec
{
    public required string Executable { get; init; }
    public ReadOnlyCollection<string> Args { get; init; } = ReadOnlyCollection<string>.Empty;
    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>();
    public string? WorkingDirectory { get; init; }
    public string HealthPath { get; init; } = QylConstants.Routes.Health;
}

public sealed record QylContainerSpec
{
    public required string Image { get; init; }
    public required int ContainerPort { get; init; }
    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>();
    public ReadOnlyCollection<string> Args { get; init; } = ReadOnlyCollection<string>.Empty;
    public ReadOnlyCollection<string> Volumes { get; init; } = ReadOnlyCollection<string>.Empty;
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
