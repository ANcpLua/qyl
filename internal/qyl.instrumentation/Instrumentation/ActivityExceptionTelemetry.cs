using ErrorAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Error.ErrorAttributes;
using ExceptionAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Exception.ExceptionAttributes;

namespace Qyl.Instrumentation.Instrumentation;

/// <summary>
///     Shared OpenTelemetry exception recording for qyl instrumentation.
/// </summary>
public static class ActivityExceptionTelemetry
{
    private const string ErrorType = ErrorAttributes.Type;
    private const string ExceptionType = ExceptionAttributes.Type;
    private const string ExceptionMessage = ExceptionAttributes.Message;
    private const string ExceptionStacktrace = ExceptionAttributes.Stacktrace;
    // exception.escaped was removed from upstream OTel semconv with no replacement;
    // kept as an inline literal to preserve qyl telemetry consumers.
    private const string ExceptionEscaped = "exception.escaped";

    public static void Record(
        Activity? activity,
        Exception exception,
        string? errorType = null,
        bool escaped = true)
    {
        if (activity is null)
            return;

        ApplyError(activity, exception, errorType);
        activity.AddEvent(new ActivityEvent("exception", tags: CreateTags(exception, escaped)));
    }

    public static void ApplyError(
        Activity? activity,
        Exception exception,
        string? errorType = null)
    {
        if (activity is null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag(ErrorType, ResolveErrorType(exception, errorType));
    }

    public static ActivityTagsCollection CreateTags(Exception exception, bool escaped = true) =>
        new()
        {
            { ExceptionType, exception.GetType().FullName },
            { ExceptionMessage, exception.Message },
            { ExceptionStacktrace, exception.ToString() },
            { ExceptionEscaped, escaped }
        };

    public static string ResolveErrorType(Exception exception, string? errorType = null) =>
        !string.IsNullOrWhiteSpace(errorType)
            ? errorType
            : exception.GetType().FullName ?? exception.GetType().Name;
}
