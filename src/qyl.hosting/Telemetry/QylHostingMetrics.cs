using Qyl.ServiceDefaults.Instrumentation;

namespace Qyl.Hosting.Telemetry;

[Meter("qyl.hosting", Version = "1.0.0")]
internal static partial class QylHostingMetrics
{
    [Counter("qyl.hosting.resource.started", Unit = "{resource}", Description = "Number of resources started")]
    public static partial void RecordResourceStarted(
        [Tag("qyl.hosting.resource.name")] string name,
        [Tag("qyl.hosting.resource.type")] string type);

    [Histogram("qyl.hosting.resource.start.duration", Unit = "s", Description = "Time to start a resource")]
    public static partial void RecordStartDuration(
        double value,
        [Tag("qyl.hosting.resource.name")] string name,
        [Tag("qyl.hosting.resource.type")] string type);

    [Histogram("qyl.hosting.resource.health_check.duration", Unit = "s", Description = "Health check wait time")]
    public static partial void RecordHealthCheckDuration(
        double value,
        [Tag("qyl.hosting.resource.name")] string name);

    [UpDownCounter("qyl.hosting.resources.active", Unit = "{resource}", Description = "Currently running resources")]
    public static partial void UpdateActiveResources(
        long value,
        [Tag("qyl.hosting.resource.name")] string name);
}
