using qyl.protocol.Attributes;

namespace Qyl.ServiceDefaults.Instrumentation;

/// <summary>
///     Shared OpenTelemetry exception recording for qyl instrumentation.
/// </summary>
public static class ActivityExceptionTelemetry
{
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
        activity.SetTag(ErrorTypeAttributes.Type, ResolveErrorType(exception, errorType));
    }

    public static ActivityTagsCollection CreateTags(Exception exception, bool escaped = true) =>
        new()
        {
            { ExceptionTypeAttributes.Type, exception.GetType().FullName },
            { ExceptionMessageAttributes.Message, exception.Message },
            { ExceptionStacktraceAttributes.Stacktrace, exception.ToString() },
            { ExceptionEscapedAttributes.Escaped, escaped }
        };

    public static string ResolveErrorType(Exception exception, string? errorType = null) =>
        !string.IsNullOrWhiteSpace(errorType)
            ? errorType
            : exception.GetType().FullName ?? exception.GetType().Name;
}
