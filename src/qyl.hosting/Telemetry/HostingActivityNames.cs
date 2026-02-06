namespace Qyl.Hosting.Telemetry;

/// <summary>
/// Span name constants for qyl.hosting activities.
/// </summary>
internal static class HostingActivityNames
{
    public const string Run = "qyl.hosting.run";
    public const string CollectorStart = "qyl.hosting.collector.start";
    public const string ResourceStart = "qyl.hosting.resource.start";
    public const string HealthCheck = "qyl.hosting.health_check";
    public const string Shutdown = "qyl.hosting.shutdown";
}

/// <summary>
/// Event name constants for qyl.hosting span events.
/// </summary>
internal static class HostingEventNames
{
    public const string ResourceReady = "resource.ready";
    public const string ResourceFailed = "resource.failed";
    public const string ShutdownStarted = "shutdown.started";
    public const string ShutdownCompleted = "shutdown.completed";
}
