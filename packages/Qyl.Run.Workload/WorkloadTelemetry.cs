namespace Qyl.Run.Workload;

/// <summary>
/// The workload's telemetry identity. Consumers own the <see cref="ActivitySource"/> and
/// <see cref="Meter"/> lifecycle by the generator's design — the SemConv source generation
/// only provides typed setters and instrument factories over the BCL surface.
/// </summary>
internal static class WorkloadTelemetry
{
    public const string SourceName = "Qyl.Run.Workload";

    public const string DefaultServiceName = "workload";

    public static readonly ActivitySource Source = new(SourceName);

    public static readonly Meter Meter = new(SourceName);
}
