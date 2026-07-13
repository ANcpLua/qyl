namespace Qyl.Run.Workload;

/// <summary>
/// The workload's telemetry identity. The workload emits only signals the Qyl collector
/// accepts today; the SemConv source generation provides typed activity setters.
/// </summary>
internal static class WorkloadTelemetry
{
    public const string SourceName = "Qyl.Run.Workload";

    public const string DefaultServiceName = "workload";

    public static readonly ActivitySource Source = new(SourceName);
}
