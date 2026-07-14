namespace Qyl.Run.Workload;

/// <summary>
/// The workload's telemetry identity. It emits only signals accepted by the Qyl collector;
/// SemConv source generation provides typed activity setters.
/// </summary>
internal static class WorkloadTelemetry
{
    public const string SourceName = "Qyl.Run.Workload";

    public const string DefaultServiceName = "workload";

    public static readonly ActivitySource Source = new(SourceName);
}
