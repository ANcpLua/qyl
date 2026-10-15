using Qyl.Instrumentation.ErrorCapture;
using Qyl.Telemetry.SemanticConventions.Names;

namespace Qyl.Collector.Tests;

/// <summary>
/// An unhandled or unobserved exception with no span current gets a span of its own on the application scope,
/// which AddQyl subscribes, and that span ends: a span nobody stops is never exported.
/// </summary>
public sealed class GlobalExceptionHooksTests
{
    [Fact]
    public void An_exception_outside_any_span_is_recorded_on_an_ended_application_span()
    {
        var stopped = new List<Activity>();
        using var listener = new ActivityListener();
        // The name, not Spans.Source: creating that source consults this callback inside Spans' type initializer.
        listener.ShouldListenTo = static source => source.Name == QylTelemetryNames.Scopes.QylTelemetryApplication;
        listener.Sample = static (ref _) => ActivitySamplingResult.AllData;
        listener.ActivityStopped = activity =>
        {
            lock (stopped)
                stopped.Add(activity);
        };
        ActivitySource.AddActivityListener(listener);

        var ambient = Activity.Current;
        Activity.Current = null;
        try
        {
            GlobalExceptionHooks.RecordGlobalException(new InvalidOperationException("boom"), "TaskScheduler.UnobservedTaskException");
        }
        finally
        {
            Activity.Current = ambient;
        }

        Activity span;
        lock (stopped)
            span = Assert.Single(stopped, static activity => activity.DisplayName == "UnhandledException");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("app", span.GetTagItem("qyl.instrumentation.domain"));
        Assert.Contains(span.Events, static activityEvent => activityEvent.Name == "exception");
    }
}
