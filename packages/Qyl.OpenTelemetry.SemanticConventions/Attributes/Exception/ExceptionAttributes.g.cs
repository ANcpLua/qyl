

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Attributes.Exception;

public static class ExceptionAttributes
{
    [global::System.Obsolete("Removed, no replacement.", false)]
    public const string Escaped = "exception.escaped";

    public const string Message = "exception.message";

    public const string Stacktrace = "exception.stacktrace";

    public const string Type = "exception.type";
}
