
using System.Collections.ObjectModel;

namespace Qyl.Host;

internal sealed record QylResource
{
    public required string Name { get; init; }

    public required QylResourceKind Kind { get; init; }

    public required int Port { get; init; }

    // Collector resources only: the OTLP receiver ports the child is pinned to via
    // QYL_OTLP_PORT / QYL_GRPC_PORT. 0 = not an OTLP receiver.
    public int OtlpHttpPort { get; init; }

    public int GrpcPort { get; init; }

    // Names of resources that must report Ready before this resource's process is launched.
    public IReadOnlyList<string> WaitsFor { get; init; } = [];

    // Null = a connection-only resource: the runner launches no child process, and readiness is
    // decided entirely by ReadinessProbe (required in that case — Build() enforces it). The MCP
    // kinds use this: the SDK transport owns the connection (and for stdio, the process).
    public QylLaunchSpec? Launch { get; init; }

    // Optional per-resource readiness override; null means the default HTTP health probe against
    // Launch.HealthPath. Set via QylResourceBuilderExtensions.WithReadinessProbe.
    public IReadinessProbe? ReadinessProbe { get; init; }
}

internal sealed record QylLaunchSpec
{
    public required string Executable { get; init; }
    public ReadOnlyCollection<string> Args { get; init; } = ReadOnlyCollection<string>.Empty;
    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>();
    public string? WorkingDirectory { get; init; }
    public string HealthPath { get; init; } = QylConstants.Routes.Health;
}

internal sealed record QylProcessCommand
{
    public required string Executable { get; init; }
    public ReadOnlyCollection<string> Args { get; init; } = ReadOnlyCollection<string>.Empty;
    public string? WorkingDirectory { get; init; }
}

internal enum ResourceLifecycle
{
    Pending,
    Starting,
    Ready,
    Stopping,
    Stopped,
    Failed
}

internal sealed record QylResourceState
{
    public required string Name { get; init; }
    public required ResourceLifecycle Lifecycle { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    public QylResourceKind? Kind { get; init; }

    public int? AllocatedPort { get; init; }
    public Uri? Endpoint { get; init; }
    public string? LastError { get; init; }
}

internal enum QylResourceKind
{
    Collector,
    Project,
    Command,
    McpStdio,
    McpHttp
}

internal static class QylResourceKindExtensions
{
    internal static string ToWireName(this QylResourceKind kind) => kind switch
    {
        QylResourceKind.Collector => "collector",
        QylResourceKind.Project => "project",
        QylResourceKind.Command => "command",
        QylResourceKind.McpStdio => "stdio",
        QylResourceKind.McpHttp => "http",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown resource kind")
    };
}
