using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.Ingestion;

namespace Qyl.Collector.Tests;

/// <summary>
/// QYL-U1: MCP emits <c>jsonrpc.request.id</c> and <c>jsonrpc.protocol.version</c>; the collector
/// allows the <c>jsonrpc.</c> prefix for spans, logs and metrics instead of dropping both keys.
/// </summary>
public sealed class SemanticPolicyJsonRpcTests
{
    [Theory]
    [InlineData("jsonrpc.request.id")]
    [InlineData("jsonrpc.protocol.version")]
    public void Jsonrpc_keys_are_allowed_on_every_signal(string key)
    {
        Assert.True(AttributeKeySets.IsSafeSpanAttribute(key));
        Assert.True(AttributeKeySets.ShouldCaptureSpanAttribute(key));
        Assert.True(AttributeKeySets.IsSafeLogAttribute(key));
        Assert.True(AttributeKeySets.IsSafeMetricAttribute(key));
    }

    [Fact]
    public void Jsonrpc_attributes_reach_the_converted_span_including_the_renamed_legacy_request_id()
    {
        var span = new Span
        {
            TraceId = ByteString.CopyFrom(new byte[16]),
            SpanId = ByteString.CopyFrom(new byte[8]),
            Name = "tools/call",
            StartTimeUnixNano = 1,
            EndTimeUnixNano = 2
        };
        span.Attributes.Add(new KeyValue { Key = "jsonrpc.protocol.version", Value = new AnyValue { StringValue = "2.0" } });
        span.Attributes.Add(new KeyValue { Key = "rpc.jsonrpc.request_id", Value = new AnyValue { StringValue = "req-7" } });

        var request = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource(),
                    ScopeSpans = { new ScopeSpans { Spans = { span } } }
                }
            }
        };

        var attributes = Assert.Single(OtlpConverter.ConvertTraceRequest(request).Spans).Attributes;
        Assert.Equal("2.0", attributes["jsonrpc.protocol.version"].AsString());
        Assert.True(DeprecatedAttributeNormalizer.TryNormalize("rpc.jsonrpc.request_id", out var renamed));
        Assert.Equal("jsonrpc.request.id", renamed);
        Assert.Equal("req-7", attributes["jsonrpc.request.id"].AsString());
        Assert.DoesNotContain("rpc.jsonrpc.request_id", attributes.Keys);
    }

    [Fact]
    public void Profiles_only_alpha_fields_from_proto_1_11_1_are_treated_as_absent()
    {
        var span = new Span
        {
            TraceId = ByteString.CopyFrom(new byte[16]),
            SpanId = ByteString.CopyFrom(new byte[8]),
            Name = "alpha-fields",
            StartTimeUnixNano = 1,
            EndTimeUnixNano = 2
        };
        span.Attributes.Add(new KeyValue { Key = "http.request.method", Value = new AnyValue { StringValueStrindex = 3 } });
        span.Attributes.Add(new KeyValue { KeyStrindex = 4, Value = new AnyValue { StringValue = "unkeyed" } });

        var request = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource(),
                    ScopeSpans = { new ScopeSpans { Spans = { span } } }
                }
            }
        };

        var attributes = Assert.Single(OtlpConverter.ConvertTraceRequest(request).Spans).Attributes;
        Assert.Equal(OtlpAttributeValue.Empty, attributes["http.request.method"]);
        Assert.Equal(2, attributes.Count); // service.name plus the key whose value is absent
    }
}
