using qyl.contracts.Attributes;

namespace qyl.mcp;

internal static class TelemetryConstants
{
    // ActivitySource name for MCP tool invocations — per qyl observability docs
    // (CLAUDE.md §Observability). Do NOT use GenAiAttributes.SourceName here:
    // that's the upstream semconv ActivitySource name for GenAI chat-client
    // spans, not MCP tool spans. Schema URL still comes from semconv 1.40.
    public static readonly ActivitySource ActivitySource = new(new ActivitySourceOptions("qyl.mcp")
    {
        Version = "1.0.0", TelemetrySchemaUrl = GenAiAttributes.SchemaUrl
    });
}
