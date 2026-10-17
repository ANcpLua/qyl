using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.Ingestion;

namespace Qyl.Collector.Tests;

/// <summary>
/// MCP's JSON-RPC attributes follow the shared persistence policy, including legacy-key normalization.
/// </summary>
[TestClass]
public sealed class SemanticPolicyJsonRpcTests
{
    [TestMethod]
    [DataRow("jsonrpc.request.id")]
    [DataRow("jsonrpc.protocol.version")]
    public void Jsonrpc_keys_follow_the_shared_persistence_policy(string key)
    {
        Assert.IsTrue(AttributeKeySets.IsPersistable(key));
        Assert.IsTrue(AttributeKeySets.ShouldCaptureSpanAttribute(key));
    }

    [TestMethod]
    public void Jsonrpc_attributes_reach_the_converted_span_including_the_renamed_legacy_request_id()
    {
        var span = new Span
        {
            TraceId = ByteString.CopyFrom(Enumerable.Range(1, 16).Select(static value => (byte)value).ToArray()),
            SpanId = ByteString.CopyFrom(Enumerable.Range(1, 8).Select(static value => (byte)value).ToArray()),
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

        var attributes = Assert.ContainsSingle(OtlpConverter.ConvertTraceRequest(request).Spans).Attributes;
        Assert.AreEqual("2.0", attributes["jsonrpc.protocol.version"].AsString());
        Assert.IsTrue(DeprecatedAttributeNormalizer.TryNormalize("rpc.jsonrpc.request_id", out var renamed));
        Assert.AreEqual("jsonrpc.request.id", renamed);
        Assert.AreEqual("req-7", attributes["jsonrpc.request.id"].AsString());
        Assert.DoesNotContain("rpc.jsonrpc.request_id", attributes.Keys);
    }

    [TestMethod]
    public void Profiles_only_alpha_fields_from_proto_1_11_1_are_treated_as_absent()
    {
        var span = new Span
        {
            TraceId = ByteString.CopyFrom(Enumerable.Range(1, 16).Select(static value => (byte)value).ToArray()),
            SpanId = ByteString.CopyFrom(Enumerable.Range(1, 8).Select(static value => (byte)value).ToArray()),
            Name = "alpha-fields",
            StartTimeUnixNano = 1,
            EndTimeUnixNano = 2
        };
        span.Attributes.Add(new KeyValue { Key = "http.request.method", Value = new AnyValue { StringValueStrindex = 3 } });
        span.Attributes.Add(new KeyValue { KeyStrindex = 4, Value = new AnyValue { StringValue = "unkeyed" } });
        var resource = new Resource { Attributes = { new KeyValue { KeyStrindex = 5 } } };
        var scope = new InstrumentationScope { Attributes = { new KeyValue { KeyStrindex = 6 } } };

        var request = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = resource,
                    ScopeSpans = { new ScopeSpans { Scope = scope, Spans = { span } } }
                }
            }
        };

        var batch = OtlpConverter.ConvertTraceRequest(request);
        Assert.AreEqual(0L, batch.RejectedSpans);
        var converted = Assert.ContainsSingle(batch.Spans);
        Assert.IsEmpty(converted.ScopeAttributes);
        var attributes = converted.Attributes;
        Assert.AreEqual(OtlpAttributeValue.Empty, attributes["http.request.method"]);
        Assert.AreEqual(2, attributes.Count); // service.name plus the key whose value is absent
    }

    [TestMethod]
    public void Profiling_key_indexes_remain_nonfatal_in_log_bodies_and_metric_metadata()
    {
        var resource = new Resource { Attributes = { new KeyValue { KeyStrindex = 2 } } };
        var scope = new InstrumentationScope { Attributes = { new KeyValue { KeyStrindex = 3 } } };
        var logs = new OpenTelemetry.Proto.Collector.Logs.V1.ExportLogsServiceRequest
        {
            ResourceLogs = { new OpenTelemetry.Proto.Logs.V1.ResourceLogs
            {
                Resource = resource,
                ScopeLogs = { new OpenTelemetry.Proto.Logs.V1.ScopeLogs
                {
                    Scope = scope,
                    LogRecords = { new OpenTelemetry.Proto.Logs.V1.LogRecord
                    {
                        Attributes = { new KeyValue { KeyStrindex = 4 } },
                        Body = new AnyValue { KvlistValue = new KeyValueList { Values =
                        {
                            new KeyValue { KeyStrindex = 5 },
                            new KeyValue { Key = "retained", Value = new AnyValue { StringValue = "original" } }
                        } } }
                    } }
                } }
            } }
        };
        var logBatch = OtlpConverter.ConvertLogs(logs, TimeProvider.System);
        Assert.AreEqual(0L, logBatch.RejectedLogRecords);
        var log = Assert.ContainsSingle(logBatch.Logs);
        Assert.IsEmpty(log.ScopeAttributes);
        var body = Assert.IsInstanceOfType<Api.Contracts.Common.AttributeKeyValueListValue>(
            Assert.IsInstanceOfType<Api.Contracts.Common.AttributeValue.ObjectValue>(log.Body.ToContract()).Value);
        Assert.HasCount(1, body.Values);
        Assert.AreEqual("original", Assert.IsInstanceOfType<Api.Contracts.Common.AttributeValue.StringValue>(body.Values["retained"]).Value);

        var metrics = new OpenTelemetry.Proto.Collector.Metrics.V1.ExportMetricsServiceRequest
        {
            ResourceMetrics = { new OpenTelemetry.Proto.Metrics.V1.ResourceMetrics
            {
                Resource = resource,
                ScopeMetrics = { new OpenTelemetry.Proto.Metrics.V1.ScopeMetrics
                {
                    Scope = scope,
                    Metrics = { new OpenTelemetry.Proto.Metrics.V1.Metric
                    {
                        Name = "profiling.forward.compatibility", Metadata = { new KeyValue { KeyStrindex = 4 } },
                        Gauge = new OpenTelemetry.Proto.Metrics.V1.Gauge
                        { DataPoints = { new OpenTelemetry.Proto.Metrics.V1.NumberDataPoint { TimeUnixNano = 1, AsInt = 7 } } }
                    } }
                } }
            } }
        };
        var metricBatch = OtlpConverter.ConvertMetrics(metrics);
        Assert.AreEqual(0L, metricBatch.RejectedDataPoints);
        var metric = Assert.ContainsSingle(metricBatch.Points);
        Assert.IsEmpty(metric.ScopeAttributes);
        Assert.IsEmpty(metric.InstrumentMetadata);
    }
}
