using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Telemetry;
using Qyl.Telemetry.SemanticConventions.Incubating.Mapping;
using Qyl.Telemetry.SemanticConventions.Incubating.Metrics;

namespace Qyl.Collector.Tests;

public sealed class SpanAttributePolicyTests
{
    [Theory]
    [InlineData("graphql.operation.name")]
    [InlineData("graphql.operation.type")]
    public void GraphQlOperationAttributes_AreCaptured(string key)
    {
        Assert.True(AttributeKeySets.IsSafeSpanAttribute(key));
        Assert.True(AttributeKeySets.ShouldCaptureSpanAttribute(key));
    }

    [Fact]
    public void GraphQlDocument_IsDeniedAsPayload()
    {
        Assert.False(AttributeKeySets.IsSafeSpanAttribute("graphql.document"));
        Assert.False(AttributeKeySets.ShouldCaptureSpanAttribute("graphql.document"));
    }

    /// <summary>
    /// One span carries every branch of the ingest policy at once: a deprecated key that is
    /// renamed, two vendor pass-through keys whose own spelling contains a denied token, an
    /// unregistered key that is dropped and counted, and two keys the privacy denials refuse.
    /// The vendor keys are the regression: the substring rule ran ahead of the vendor check and
    /// dropped twelve of the pinned vendor keys because they contain "message" or "result".
    /// </summary>
    [Fact]
    public void Vendor_pass_through_survives_the_token_denial_while_privacy_denials_still_win()
    {
        var droppedNamespaces = new ConcurrentBag<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, activeListener) =>
        {
            if (instrument.Meter.Name == QylTelemetry.ServiceName &&
                instrument.Name == QylIncubatingMetricDefinitions.QylCollectorAttributesDropped.Name)
            {
                activeListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == CollectorSemanticAttributeCatalog.QylAttributeNamespace &&
                    tag.Value is string attributeNamespace)
                {
                    droppedNamespaces.Add(attributeNamespace);
                }
            }
        });
        listener.Start();

        var span = new Span
        {
            TraceId = ByteString.CopyFrom(new byte[16]),
            SpanId = ByteString.CopyFrom(new byte[8]),
            Name = "ingest-policy",
            StartTimeUnixNano = 1,
            EndTimeUnixNano = 2
        };
        span.Attributes.Add(new[]
        {
            StringAttribute("http.method", "GET"),
            StringAttribute("messaging.masstransit.message_id", "mt-1"),
            StringAttribute("nservicebus.message_id", "nsb-1"),
            StringAttribute("foo.bar", "unregistered"),
            BoolAttribute("exception.escaped", true),
            StringAttribute("exception.message", "boom"),
            StringAttribute("user.id", "u-1")
        });

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

        Assert.Equal("GET", attributes[CollectorSemanticAttributeCatalog.HttpRequestMethod].AsString());
        Assert.DoesNotContain("http.method", attributes.Keys);
        Assert.Equal("mt-1", attributes["messaging.masstransit.message_id"].AsString());
        Assert.Equal("nsb-1", attributes["nservicebus.message_id"].AsString());
        Assert.Contains("exception.escaped", attributes.Keys);

        Assert.DoesNotContain("foo.bar", attributes.Keys);
        Assert.Equal("other", AttributeMapping.NamespaceOf("foo.bar"));
        Assert.Contains("other", droppedNamespaces);

        Assert.DoesNotContain("exception.message", attributes.Keys);
        Assert.DoesNotContain("user.id", attributes.Keys);

        // The waiver is scoped: a vendor key clears the substring rule, an exact denial
        // (exception.message) and a prefix denial (user.) refuse the key whether or not the
        // vendor list would have claimed it.
        Assert.True(AttributeMapping.IsVendorPassThrough("messaging.masstransit.message_id"));
        Assert.True(AttributeMapping.IsVendorPassThrough("nservicebus.message_id"));
        Assert.True(AttributeKeySets.IsSafeSpanAttribute("messaging.masstransit.message_id"));
        Assert.True(AttributeKeySets.IsSafeSpanAttribute("nservicebus.message_id"));
        Assert.False(AttributeKeySets.IsSafeSpanAttribute("exception.message"));
        Assert.False(AttributeKeySets.IsSafeSpanAttribute("user.id"));
    }

    private static KeyValue StringAttribute(string key, string value) =>
        new() { Key = key, Value = new AnyValue { StringValue = value } };

    private static KeyValue BoolAttribute(string key, bool value) =>
        new() { Key = key, Value = new AnyValue { BoolValue = value } };
}
