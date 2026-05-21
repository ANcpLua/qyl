// Consumer fixture for the complete source-generation surface. The project
// builds this file under every supported TFM so generator output is checked
// in the consumer's compilation, not only in the analyzer's netstandard2.0
// build. Every marker family is represented, and both stable and incubating
// tiers are bound where the v1.41.0 registry has useful rows.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

namespace Qyl.SemConv.Smoke.Matrix;

[SemanticConventionAttributes("http")]
internal static partial class HttpAttributes;

[SemanticConventionIncubatingAttributes("http")]
internal static partial class HttpIncubatingAttributes;

[SemanticConventionMetrics("http.server")]
internal static partial class HttpServerMetrics;

[SemanticConventionIncubatingMetrics("http.server")]
internal static partial class HttpServerIncubatingMetrics;

[SemanticConventionIncubatingEvents("session")]
internal static partial class SessionEvents;

[SemanticConventionMeters("http.server")]
internal static partial class HttpServerMeters;

[SemanticConventionIncubatingMeters("http.server")]
internal static partial class HttpServerIncubatingMeters;

[SemanticConventionActivities("http")]
internal static partial class HttpActivityExtensions;

[SemanticConventionIncubatingActivities("http")]
internal static partial class HttpIncubatingActivityExtensions;

internal static class Anchor
{
    public const string StableAttribute = HttpAttributes.AttributeHttpRequestMethod;
    public const string IncubatingAttribute = HttpIncubatingAttributes.AttributeHttpConnectionState;
    public const string StableMetricName = HttpServerMetrics.HttpServerRequestDurationDescriptor.Name;
    public const string IncubatingMetricName = HttpServerIncubatingMetrics.HttpServerActiveRequestsDescriptor.Name;
    public const string IncubatingEventName = SessionEvents.EventSessionStart;

    public static SessionEvents.SessionStartPayload CreatePayload() => new()
    {
        SessionId = "session-1",
        SessionPreviousId = "session-0"
    };

    public static Histogram<double> CreateStableHistogram(Meter meter) =>
        HttpServerMeters.CreateHttpServerRequestDurationHistogram(meter);

    public static UpDownCounter<long> CreateIncubatingUpDownCounter(Meter meter) =>
        HttpServerIncubatingMeters.CreateHttpServerActiveRequestsUpdowncounter(meter);

    public static Activity ApplyStableTag(Activity activity) =>
        HttpActivityExtensions.SetHttpRequestMethod(
            activity,
            HttpActivityExtensions.HttpRequestMethodValues.Get);

    public static Activity ApplyIncubatingTag(Activity activity) =>
        HttpIncubatingActivityExtensions.SetHttpConnectionState(
            activity,
            HttpIncubatingActivityExtensions.HttpConnectionStateValues.Active);
}
