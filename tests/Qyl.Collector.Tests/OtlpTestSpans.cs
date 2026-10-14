using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OtlpResourceSpans = OpenTelemetry.Proto.Trace.V1.ResourceSpans;
using OtlpScopeSpans = OpenTelemetry.Proto.Trace.V1.ScopeSpans;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Qyl.Collector.Tests;

/// <summary>One-span OTLP trace exports, shaped the way an SDK exporter sends them.</summary>
internal static class OtlpTestSpans
{
    private static ulong Now => (ulong)DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeMilliseconds() * 1_000_000;

    /// <summary>
    /// One internal span of service <c>app-service</c> on the scope <paramref name="scopeName"/>, carrying
    /// <paramref name="attributes"/> in wire order.
    /// </summary>
    internal static ExportTraceServiceRequest Request(string scopeName, IEnumerable<KeyValue> attributes, string name = "work")
    {
        var start = Now;
        var span = new OtlpSpan
        {
            TraceId = ByteString.CopyFrom([.. Enumerable.Range(1, 16).Select(static i => (byte)i)]),
            SpanId = ByteString.CopyFrom([.. Enumerable.Range(1, 8).Select(static i => (byte)i)]),
            Name = name,
            Kind = OtlpSpan.Types.SpanKind.Internal,
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
                    Resource = new Resource { Attributes = { String("service.name", "app-service") } },
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
