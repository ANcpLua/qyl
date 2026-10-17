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

[TestClass]
public sealed class SpanAttributePolicyTests
{
    [TestMethod]
    [DataRow("graphql.operation.name")]
    [DataRow("graphql.operation.type")]
    public void GraphQlOperationAttributes_AreCaptured(string key)
    {
        Assert.IsTrue(AttributeKeySets.IsPersistable(key));
        Assert.IsTrue(AttributeKeySets.ShouldCaptureSpanAttribute(key));
    }

    [TestMethod]
    public void GraphQlDocument_IsDeniedAsPayload()
    {
        Assert.IsFalse(AttributeKeySets.IsPersistable("graphql.document"));
        Assert.IsFalse(AttributeKeySets.ShouldCaptureSpanAttribute("graphql.document"));
    }

    /// <summary>
    /// One span carries every branch of the ingest policy at once: a deprecated key that is
    /// renamed, two vendor pass-through keys whose own spelling contains a denied token, an
    /// unregistered key that is dropped and counted, and two keys the privacy denials refuse.
    /// The vendor keys are the regression: the substring rule ran ahead of the vendor check and
    /// dropped twelve of the pinned vendor keys because they contain "message" or "result".
    /// </summary>
    [TestMethod]
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
                if (tag is { Key: CollectorSemanticAttributeCatalog.QylAttributeNamespace, Value: string attributeNamespace })
                {
                    droppedNamespaces.Add(attributeNamespace);
                }
            }
        });
        listener.Start();

        var span = new Span
        {
            TraceId = ByteString.CopyFrom(Enumerable.Range(1, 16).Select(static value => (byte)value).ToArray()),
            SpanId = ByteString.CopyFrom(Enumerable.Range(1, 8).Select(static value => (byte)value).ToArray()),
            Name = "ingest-policy",
            StartTimeUnixNano = 1,
            EndTimeUnixNano = 2
        };
        span.Attributes.Add([

            StringAttribute("http.method", "GET"),
            StringAttribute("messaging.masstransit.message_id", "mt-1"),
            StringAttribute("nservicebus.message_id", "nsb-1"),
            StringAttribute("foo.bar", "unregistered"),
            BoolAttribute("exception.escaped", true),
            StringAttribute("exception.message", "boom"),
            StringAttribute("code.stacktrace", "denied stack"),
            StringAttribute("custom.api_key", "denied credential"),
            StringAttribute("user.id", "u-1")
        ]);

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

        var attributes = Assert.ContainsSingle(OtlpConverter.ConvertTraceRequest(request).Spans).Attributes;

        Assert.AreEqual("GET", attributes[CollectorSemanticAttributeCatalog.HttpRequestMethod].AsString());
        Assert.DoesNotContain("http.method", attributes.Keys);
        Assert.AreEqual("mt-1", attributes["messaging.masstransit.message_id"].AsString());
        Assert.AreEqual("nsb-1", attributes["nservicebus.message_id"].AsString());
        Assert.Contains("exception.escaped", attributes.Keys);

        Assert.AreEqual("unregistered", attributes["foo.bar"].AsString());
        Assert.AreEqual("other", AttributeMapping.NamespaceOf("foo.bar"));
        Assert.Contains("other", droppedNamespaces);

        Assert.AreEqual("boom", attributes["exception.message"].AsString());
        Assert.DoesNotContain("code.stacktrace", attributes.Keys);
        Assert.DoesNotContain("custom.api_key", attributes.Keys);
        Assert.DoesNotContain("user.id", attributes.Keys);

        // The waiver is scoped: a vendor key clears the substring rule, an exact denial
        // (code.stacktrace) and a prefix denial (user.) refuse the key whether or not the
        // vendor list would have claimed it.
        Assert.Contains("messaging.masstransit.message_id", (IReadOnlySet<string>)CollectorSemanticAttributeCatalog.VendorKeys);
        Assert.Contains("nservicebus.message_id", (IReadOnlySet<string>)CollectorSemanticAttributeCatalog.VendorKeys);
        Assert.IsTrue(AttributeKeySets.IsPersistable("messaging.masstransit.message_id"));
        Assert.IsTrue(AttributeKeySets.IsPersistable("nservicebus.message_id"));
        Assert.IsTrue(AttributeKeySets.IsPersistable("exception.message"));
        Assert.IsFalse(AttributeKeySets.IsPersistable("code.stacktrace"));
        Assert.IsFalse(AttributeKeySets.IsPersistable("user.id"));
    }

    private static KeyValue StringAttribute(string key, string value) =>
        new() { Key = key, Value = new AnyValue { StringValue = value } };

    private static KeyValue BoolAttribute(string key, bool value) =>
        new() { Key = key, Value = new AnyValue { BoolValue = value } };
}
