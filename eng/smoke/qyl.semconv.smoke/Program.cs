// Phase 2/4 dogfooding consumer for
// Qyl.OpenTelemetry.SemanticConventions.SourceGeneration. Each marker exercises
// one surface area of the generator pack; the binary prints the emitted
// constants/factories so a smoke test can grep the output for the expected
// tokens.
//
// PR-A : SemanticConvention[Incubating]Attributes → const string per attribute key.
// PR-B : SemanticConventionMetrics     → const string + descriptor partial class.
// PR-C : SemanticConventionEvents      → const string + payload record struct.
// PR-D : SemanticConventionMeters      → typed Meter.Create<Instrument> factories.
// PR-E : SemanticConventionActivities  → typed Activity.SetTag setters.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
using Qyl.SemConv.Smoke;

// PR-A: attribute-key constants. The marker is conditional, so it disappears
// from the consumer's metadata under default compilation flags (no
// QYL_SEMCONV_USAGES define), but the generated partial class with const
// strings remains.
Console.WriteLine($"const: {DiskAttributes.AttributeDiskIoDirection}");

// PR-B: metric descriptors. The generated HttpServerRequestDurationDescriptor
// nested type exposes Name/Unit/Instrument/Brief constants. Building a
// histogram from these constants verifies the metric naming surface is
// usable from a net10.0 + AOT consumer.
using var meter = new Meter("qyl.semconv.smoke");
var rawHistogram = meter.CreateHistogram<double>(
    name: HttpServerMetrics.HttpServerRequestDurationDescriptor.Name,
    unit: HttpServerMetrics.HttpServerRequestDurationDescriptor.Unit,
    description: HttpServerMetrics.HttpServerRequestDurationDescriptor.Brief);
rawHistogram.Record(0.123);
Console.WriteLine($"metric: {HttpServerMetrics.HttpServerRequestDurationDescriptor.Name} ({HttpServerMetrics.HttpServerRequestDurationDescriptor.Unit})");

// PR-C: event-name constants + payload structs. The payload record struct is
// trim/AOT-safe by construction (readonly record struct, no reflection-
// dependent surface). Constructing one inline proves the emitted struct
// is part of the consumer's compilation.
var payload = new SessionEvents.SessionStartPayload
{
    SessionId = "session-abc",
    SessionPreviousId = "session-xyz"
};
Console.WriteLine($"event: {SessionEvents.EventSessionStart} payload.SessionId={payload.SessionId}");

// PR-D: typed Meter.Create<Instrument> extension factories. Each registry
// metric becomes a strongly-typed factory that bakes name/unit/description
// at the call site — no risk of mismatched name strings between caller and
// the spec.
var typedHistogram = meter.CreateHttpServerRequestDurationHistogram();
typedHistogram.Record(0.456);
Console.WriteLine($"meter-ext: typed factory emitted Histogram<double> for http.server.request.duration");

// PR-E: typed Activity.SetTag setters. Each registry attribute becomes a
// SetXxx extension on Activity — same correctness guarantee as PR-D applied
// to span attributes.
using var activitySource = new ActivitySource("qyl.semconv.smoke");
using var activity = activitySource.StartActivity("http.client.request");
activity?.SetHttpRequestMethod(HttpActivityExtensions.HttpRequestMethodValues.Get);
activity?.SetHttpRoute("/users/{userId}");
Console.WriteLine($"activity-ext: typed Set* applied method={HttpActivityExtensions.HttpRequestMethodValues.Get}");

return 0;

namespace Qyl.SemConv.Smoke
{
    // PR-A surface: a marker on a partial static class with the prefix string.
    // `disk.*` is non-stable in semconv v1.41.0, so the incubating marker is the
    // honest surface for this byte-identity smoke.
    [SemanticConventionIncubatingAttributes("disk")]
    internal static partial class DiskAttributes;

    // PR-B surface: emits the metric-name constant + descriptor partial class.
    [SemanticConventionMetrics("http.server")]
    internal static partial class HttpServerMetrics;

    // PR-C surface: emits the event-name constant + the payload record struct
    // (declared at namespace scope, not inside the partial — see EventsEmitter).
    // session.start/session.end are development-stability in v1.41.0, so the
    // Incubating marker is required to make them visible on the smoke surface.
    [SemanticConventionIncubatingEvents("session")]
    internal static partial class SessionEvents;

    // PR-D surface: emits Meter.Create<Instrument> extension methods. Marker
    // attaches to a partial that becomes the extension-container class.
    [SemanticConventionMeters("http.server")]
    internal static partial class HttpServerMeters;

    // PR-E surface: emits Activity.Set<Attr> extension methods + nested
    // HttpRequestMethodValues for the enum-typed attribute.
    [SemanticConventionActivities("http")]
    internal static partial class HttpActivityExtensions;
}
