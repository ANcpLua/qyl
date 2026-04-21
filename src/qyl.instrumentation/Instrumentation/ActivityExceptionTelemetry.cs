namespace Qyl.Instrumentation.Instrumentation;

/// <summary>
///     Shared OpenTelemetry exception recording for qyl instrumentation.
///     Semconv keys inlined as literals — OTel semconv 1.40 stable section for exception.* / error.type.
/// </summary>
public static class ActivityExceptionTelemetry
{
    // OTel semconv 1.40 — stable
    private const string ErrorType = "error.type";
    private const string ExceptionType = "exception.type";
    private const string ExceptionMessage = "exception.message";
    private const string ExceptionStacktrace = "exception.stacktrace";
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
