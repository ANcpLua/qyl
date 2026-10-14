namespace Qyl.Run.Workload;

internal static class WorkloadTelemetry
{
    public const string SourceName = "Qyl.Run.Workload";

    public const string DefaultServiceName = "workload";

    // The demo's server and chat spans. No starter covers them (frameworks emit http.server and
    // gen_ai inference spans themselves); the database span comes from Spans.DbClient.
    public static readonly ActivitySource Source = new(SourceName);
}
