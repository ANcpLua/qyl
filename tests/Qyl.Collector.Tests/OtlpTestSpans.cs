using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OtlpStatus = OpenTelemetry.Proto.Trace.V1.Status;
using OtlpResourceSpans = OpenTelemetry.Proto.Trace.V1.ResourceSpans;
using OtlpScopeSpans = OpenTelemetry.Proto.Trace.V1.ScopeSpans;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Qyl.Collector.Tests;

/// <summary>One-span OTLP trace exports, shaped the way an SDK exporter sends them.</summary>
internal static class OtlpTestSpans
{
    private const ulong Now = CollectorApiTestHost.FixtureUnixNano - 120_000_000_000;

    /// <summary>
    /// One span of kind <paramref name="kind"/> (internal unless named) and service <paramref name="serviceName"/> on the scope
    /// <paramref name="scopeName"/>, carrying <paramref name="attributes"/> in wire order and <paramref name="status"/> when given.
    /// </summary>
    internal static ExportTraceServiceRequest Request(
        string scopeName,
        IEnumerable<KeyValue> attributes,
        string name = "work",
        string serviceName = "app-service",
        OtlpSpan.Types.SpanKind kind = OtlpSpan.Types.SpanKind.Internal,
        OtlpStatus? status = null)
    {
        var start = Now;
        var span = new OtlpSpan
        {
            TraceId = ByteString.CopyFrom([.. Enumerable.Range(1, 16).Select(static i => (byte)i)]),
            SpanId = ByteString.CopyFrom([.. Enumerable.Range(1, 8).Select(static i => (byte)i)]),
            Name = name,
            Kind = kind,
            Status = status,
            StartTimeUnixNano = start,
            EndTimeUnixNano = start + 1_000_000
        };
        span.Attributes.Add(attributes);

        return new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new OtlpResourceSpans
                {
                    Resource = new Resource { Attributes = { String("service.name", serviceName) } },
                    ScopeSpans = { new OtlpScopeSpans { Scope = new InstrumentationScope { Name = scopeName }, Spans = { span } } }
                }
            }
        };
    }

    internal static KeyValue String(string key, string value) => new() { Key = key, Value = new AnyValue { StringValue = value } };

    internal static KeyValue Int(string key, long value) => new() { Key = key, Value = new AnyValue { IntValue = value } };

    internal static KeyValue Double(string key, double value) => new() { Key = key, Value = new AnyValue { DoubleValue = value } };

    internal static KeyValue Bool(string key, bool value) => new() { Key = key, Value = new AnyValue { BoolValue = value } };
}
