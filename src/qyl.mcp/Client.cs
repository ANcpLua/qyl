using qyl.protocol.Attributes;

namespace qyl.mcp;

internal static class TelemetryConstants
{
    // .NET 10: ActivitySourceOptions with OTel 1.40 schema URL
    public static readonly ActivitySource ActivitySource = new(new ActivitySourceOptions(GenAiAttributes.SourceName)
    {
        Version = "1.0.0", TelemetrySchemaUrl = GenAiAttributes.SchemaUrl
    });
}
