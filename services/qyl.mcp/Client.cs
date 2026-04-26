using Qyl.OpenTelemetry.SemanticConventions.Incubating;

namespace qyl.mcp;

internal static class TelemetryConstants
{
    // ActivitySource name for MCP tool invocations — per qyl observability docs
    // (CLAUDE.md §Observability). Schema URL comes from semconv 1.40.
    public static readonly ActivitySource ActivitySource = new(new ActivitySourceOptions("qyl.mcp")
    {
        Version = "1.0.0", TelemetrySchemaUrl = SchemaUrl.Current
    });
}
