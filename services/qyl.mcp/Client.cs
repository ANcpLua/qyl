using Qyl.Instrumentation.Instrumentation;

namespace qyl.mcp;

/// Local handle to the shared MCP ActivitySource so qyl.mcp doesn't reach into
/// <see cref="ActivitySources" /> at every call site. The source itself is the
/// canonical one defined in <c>internal/qyl.instrumentation</c> and registered
/// by <c>AddQylServiceDefaults</c>; mirroring it here would create a parallel
/// source the OTel pipeline never subscribes to.
internal static class TelemetryConstants
{
    public static ActivitySource ActivitySource => ActivitySources.McpSource;
}
