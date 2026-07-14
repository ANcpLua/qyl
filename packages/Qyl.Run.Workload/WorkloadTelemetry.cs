namespace Qyl.Run.Workload;

internal static class WorkloadTelemetry
{
    public const string SourceName = "Qyl.Run.Workload";

    public const string DefaultServiceName = "workload";

    public static readonly ActivitySource Source = new(SourceName);
}
