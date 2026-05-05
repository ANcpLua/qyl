

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Attributes.Otel;

public static class OtelAttributes
{
    public const string EventName = "otel.event.name";

    public const string ScopeName = "otel.scope.name";

    public const string ScopeVersion = "otel.scope.version";

    public const string StatusCode = "otel.status_code";

    public static class StatusCodeValues
    {
        public const string Error = "ERROR";

        public const string Ok = "OK";
    }

    public const string StatusDescription = "otel.status_description";
}
