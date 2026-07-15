using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Mapping;
using Qyl.Collector.Hosting;
using Qyl.Collector.Storage;
using OtlpLogRecord = OpenTelemetry.Proto.Logs.V1.LogRecord;
using OtlpExemplar = OpenTelemetry.Proto.Metrics.V1.Exemplar;
using OtlpMetric = OpenTelemetry.Proto.Metrics.V1.Metric;
using OtlpResourceMetrics = OpenTelemetry.Proto.Metrics.V1.ResourceMetrics;
using OtlpScopeMetrics = OpenTelemetry.Proto.Metrics.V1.ScopeMetrics;
using OtlpProfile = OpenTelemetry.Proto.Profiles.V1Development.Profile;
using OtlpProfileLink = OpenTelemetry.Proto.Profiles.V1Development.Link;
using OtlpProfilesDictionary = OpenTelemetry.Proto.Profiles.V1Development.ProfilesDictionary;
using OtlpResourceLogs = OpenTelemetry.Proto.Logs.V1.ResourceLogs;
using OtlpResourceProfiles = OpenTelemetry.Proto.Profiles.V1Development.ResourceProfiles;
using OtlpScopeLogs = OpenTelemetry.Proto.Logs.V1.ScopeLogs;
using OtlpScopeProfiles = OpenTelemetry.Proto.Profiles.V1Development.ScopeProfiles;
using RpcStatus = Google.Rpc.Status;

namespace Qyl.Collector.Tests;

public sealed class OtlpWireTests
{
    [Theory]
    [InlineData("application/json", "Json")]
    [InlineData("APPLICATION/JSON; charset=utf-8", "Json")]
    [InlineData("application/x-protobuf", "Protobuf")]
    [InlineData("Application/X-Protobuf; version=1", "Protobuf")]
    public void Otlp_content_type_accepts_only_the_declared_media_types_with_optional_parameters(
        string contentType,
        string expected)
    {
        Assert.Equal(expected, OtlpPayloadParser.GetEncoding(contentType).ToString());
    }

    [Theory]
    [InlineData("application/jsonp")]
    [InlineData("application/json-patch+json")]
    [InlineData("application/x-protobufevil")]
    public async Task Otlp_trace_wire_rejects_media_type_prefixes_with_415(string contentType)
    {
        await using var store = new DuckDbStore(":memory:");
        var context = NewOtlpEndpointContext(contentType, new ExportTraceServiceRequest().ToByteArray());
        var result = await CollectorEndpointExtensions.IngestOtlpTracesAsync(
            context,
            store,
            TestContext.Current.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, context.Response.StatusCode);
        Assert.Equal(OtlpPayloadParser.JsonContentType, context.Response.ContentType);
        var status = JsonParser.Default.Parse<RpcStatus>(
            Encoding.UTF8.GetString(ResponseBytes(context)));
        Assert.Equal(
            "Content-Type must be application/x-protobuf or application/json; Content-Encoding must be gzip, identity, or absent.",
            status.Message);
    }

    [Fact]
    public async Task Official_protobuf_trace_decodes_and_converts_without_a_qyl_wire_mirror()
    {
        var traceId = Enumerable.Range(0, 16).Select(static value => (byte)value).ToArray();
        var spanId = Enumerable.Range(16, 8).Select(static value => (byte)value).ToArray();
        var requestMessage = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue
                            {
                                Key = "service.name",
                                Value = new AnyValue { StringValue = "wire-test" }
                            }
                        }
                    },
                    ScopeSpans =
                    {
                        new ScopeSpans
                        {
                            Spans =
                            {
                                new Span
                                {
                                    TraceId = ByteString.CopyFrom(traceId),
                                    SpanId = ByteString.CopyFrom(spanId),
                                    Name = "programmatic-operation",
                                    Kind = Span.Types.SpanKind.Server,
                                    StartTimeUnixNano = 1_000,
                                    EndTimeUnixNano = 2_000,
                                    Attributes =
                                    {
                                        new KeyValue
                                        {
                                            Key = "server.port",
                                            Value = new AnyValue { IntValue = 5100 }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var payload = requestMessage.ToByteArray();
        var context = new DefaultHttpContext();
        context.Request.ContentType = OtlpPayloadParser.ProtobufContentType;
        context.Request.ContentLength = payload.Length;
        context.Request.Body = new MemoryStream(payload);

        var encoding = OtlpPayloadParser.GetEncoding(context.Request.ContentType);
        var parsed = await OtlpPayloadParser.ParseTraceRequestAsync(
            context.Request, encoding, TestContext.Current.CancellationToken);
        var batch = OtlpConverter.ConvertTraceRequest(parsed);

        var span = Assert.Single(batch.Spans);
        Assert.Equal(Convert.ToHexString(traceId).ToLowerInvariant(), span.TraceId);
        Assert.Equal(Convert.ToHexString(spanId).ToLowerInvariant(), span.SpanId);
        Assert.Equal("wire-test", span.ServiceName);
        Assert.Equal("programmatic-operation", span.Name);
        Assert.Equal(5100L, span.Attributes["server.port"].AsInt64());
    }

    [Fact]
    public async Task Official_protobuf_trace_decodes_from_gzip_with_the_same_bounded_parser()
    {
        var requestMessage = new ExportTraceServiceRequest
        {
            ResourceSpans = { new ResourceSpans() }
        };
        var protobuf = requestMessage.ToByteArray();
        await using var compressed = new MemoryStream();
        await using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            await gzip.WriteAsync(protobuf, TestContext.Current.CancellationToken);
        compressed.Position = 0;

        var context = new DefaultHttpContext();
        context.Request.ContentType = OtlpPayloadParser.ProtobufContentType;
        context.Request.Headers.ContentEncoding = "gzip";
        context.Request.ContentLength = compressed.Length;
        context.Request.Body = compressed;

        var parsed = await OtlpPayloadParser.ParseTraceRequestAsync(
            context.Request,
            OtlpPayloadParser.GetEncoding(context.Request.ContentType),
            TestContext.Current.CancellationToken);

        Assert.Single(parsed.ResourceSpans);
        Assert.Equal(0, compressed.Position);
    }

    [Fact]
    public async Task Otlp_json_ignores_forward_compatible_unknown_fields()
    {
        var requestMessage = new ExportTraceServiceRequest
        {
            ResourceSpans = { new ResourceSpans() }
        };
        var json = JsonNode.Parse(JsonFormatter.Default.Format(requestMessage))!.AsObject();
        json["futureSignalField"] = new JsonObject
        {
            ["revision"] = 2,
            ["traceId"] = "future-trace-id-is-not-an-otlp-id",
            ["nested"] = new JsonObject
            {
                ["spanId"] = "future-span-id-is-not-an-otlp-id",
                ["profileId"] = "future-profile-id-is-not-an-otlp-id"
            }
        };
        var payload = Encoding.UTF8.GetBytes(json.ToJsonString());

        var context = new DefaultHttpContext();
        context.Request.ContentType = OtlpPayloadParser.JsonContentType;
        context.Request.ContentLength = payload.Length;
        context.Request.Body = new MemoryStream(payload);

        var parsed = await OtlpPayloadParser.ParseTraceRequestAsync(
            context.Request,
            OtlpPayloadEncoding.Json,
            TestContext.Current.CancellationToken);

        Assert.Single(parsed.ResourceSpans);
    }

    [Fact]
    public async Task Otlp_json_normalizes_and_validates_ids_at_known_trace_paths()
    {
        var traceId = SequentialBytes(0, 16);
        var spanId = SequentialBytes(16, 8);
        var parentSpanId = SequentialBytes(24, 8);
        var linkedTraceId = SequentialBytes(32, 16);
        var linkedSpanId = SequentialBytes(48, 8);
        var request = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    ScopeSpans =
                    {
                        new ScopeSpans
                        {
                            Spans =
                            {
                                new Span
                                {
                                    TraceId = ByteString.CopyFrom(traceId),
                                    SpanId = ByteString.CopyFrom(spanId),
                                    ParentSpanId = ByteString.CopyFrom(parentSpanId),
                                    Name = "known-trace-path",
                                    Links =
                                    {
                                        new Span.Types.Link
                                        {
                                            TraceId = ByteString.CopyFrom(linkedTraceId),
                                            SpanId = ByteString.CopyFrom(linkedSpanId)
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
        var json = OfficialJson(request);
        var spanJson = json["resourceSpans"]![0]!["scopeSpans"]![0]!["spans"]![0]!;
        spanJson["traceId"] = Convert.ToHexStringLower(traceId);
        spanJson["spanId"] = Convert.ToHexStringLower(spanId);
        spanJson["parentSpanId"] = Convert.ToHexStringLower(parentSpanId);
        spanJson["links"]![0]!["traceId"] = Convert.ToHexStringLower(linkedTraceId);
        spanJson["links"]![0]!["spanId"] = Convert.ToHexStringLower(linkedSpanId);

        var parsed = await OtlpPayloadParser.ParseTraceRequestAsync(
            NewJsonRequest(json),
            OtlpPayloadEncoding.Json,
            TestContext.Current.CancellationToken);

        var span = Assert.Single(Assert.Single(Assert.Single(parsed.ResourceSpans).ScopeSpans).Spans);
        Assert.Equal(traceId, span.TraceId.ToByteArray());
        Assert.Equal(spanId, span.SpanId.ToByteArray());
        Assert.Equal(parentSpanId, span.ParentSpanId.ToByteArray());
        var link = Assert.Single(span.Links);
        Assert.Equal(linkedTraceId, link.TraceId.ToByteArray());
        Assert.Equal(linkedSpanId, link.SpanId.ToByteArray());

        spanJson["traceId"] = "not-hex";
        await Assert.ThrowsAsync<InvalidDataException>(() => OtlpPayloadParser.ParseTraceRequestAsync(
            NewJsonRequest(json),
            OtlpPayloadEncoding.Json,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Otlp_json_normalizes_known_log_and_profile_ids()
    {
        var logTraceId = SequentialBytes(64, 16);
        var logSpanId = SequentialBytes(80, 8);
        var logRequest = new ExportLogsServiceRequest
        {
            ResourceLogs =
            {
                new OtlpResourceLogs
                {
                    ScopeLogs =
                    {
                        new OtlpScopeLogs
                        {
                            LogRecords =
                            {
                                new OtlpLogRecord
                                {
                                    TraceId = ByteString.CopyFrom(logTraceId),
                                    SpanId = ByteString.CopyFrom(logSpanId)
                                }
                            }
                        }
                    }
                }
            }
        };
        var logJson = OfficialJson(logRequest);
        var logJsonRecord = logJson["resourceLogs"]![0]!["scopeLogs"]![0]!["logRecords"]![0]!;
        logJsonRecord["traceId"] = Convert.ToHexStringLower(logTraceId);
        logJsonRecord["spanId"] = Convert.ToHexStringLower(logSpanId);

        var logs = await OtlpPayloadParser.ParseLogsRequestAsync(
            NewJsonRequest(logJson),
            OtlpPayloadEncoding.Json,
            TestContext.Current.CancellationToken);
        var log = Assert.Single(Assert.Single(Assert.Single(logs.ResourceLogs).ScopeLogs).LogRecords);
        Assert.Equal(logTraceId, log.TraceId.ToByteArray());
        Assert.Equal(logSpanId, log.SpanId.ToByteArray());

        var profileId = SequentialBytes(88, 16);
        var linkedTraceId = SequentialBytes(104, 16);
        var linkedSpanId = SequentialBytes(120, 8);
        var profileRequest = new ExportProfilesServiceRequest
        {
            ResourceProfiles =
            {
                new OtlpResourceProfiles
                {
                    ScopeProfiles =
                    {
                        new OtlpScopeProfiles
                        {
                            Profiles =
                            {
                                new OtlpProfile
                                {
                                    ProfileId = ByteString.CopyFrom(profileId)
                                }
                            }
                        }
                    }
                }
            },
            Dictionary = new OtlpProfilesDictionary
            {
                LinkTable =
                {
                    new OtlpProfileLink
                    {
                        TraceId = ByteString.CopyFrom(linkedTraceId),
                        SpanId = ByteString.CopyFrom(linkedSpanId)
                    }
                }
            }
        };
        var profileJson = OfficialJson(profileRequest);
        var profileJsonValue = profileJson["resourceProfiles"]![0]!["scopeProfiles"]![0]!["profiles"]![0]!;
        profileJsonValue["profileId"] = Convert.ToHexStringLower(profileId);
        var profileJsonLink = profileJson["dictionary"]!["linkTable"]![0]!;
        profileJsonLink["traceId"] = Convert.ToHexStringLower(linkedTraceId);
        profileJsonLink["spanId"] = Convert.ToHexStringLower(linkedSpanId);

        var profiles = await OtlpPayloadParser.ParseProfilesRequestAsync(
            NewJsonRequest(profileJson),
            OtlpPayloadEncoding.Json,
            TestContext.Current.CancellationToken);
        var profile = Assert.Single(
            Assert.Single(Assert.Single(profiles.ResourceProfiles).ScopeProfiles).Profiles);
        Assert.Equal(profileId, profile.ProfileId.ToByteArray());
        var profileLink = Assert.Single(profiles.Dictionary.LinkTable);
        Assert.Equal(linkedTraceId, profileLink.TraceId.ToByteArray());
        Assert.Equal(linkedSpanId, profileLink.SpanId.ToByteArray());
    }

    [Fact]
    public async Task Otlp_json_normalizes_metric_exemplar_ids_without_touching_unrelated_fields()
    {
        var traceId = SequentialBytes(128, 16);
        var spanId = SequentialBytes(144, 8);
        var request = new ExportMetricsServiceRequest
        {
            ResourceMetrics =
            {
                new OtlpResourceMetrics
                {
                    ScopeMetrics =
                    {
                        new OtlpScopeMetrics
                        {
                            Metrics =
                            {
                                new OtlpMetric
                                {
                                    Name = "gen_ai.client.token.usage",
                                    Unit = "{token}",
                                    Histogram = new Histogram
                                    {
                                        AggregationTemporality = AggregationTemporality.Cumulative,
                                        DataPoints =
                                        {
                                            new HistogramDataPoint
                                            {
                                                TimeUnixNano = 1_000,
                                                Count = 1,
                                                Sum = 1,
                                                BucketCounts = { 1UL },
                                                Exemplars =
                                                {
                                                    new OtlpExemplar
                                                    {
                                                        TimeUnixNano = 999,
                                                        AsInt = 1,
                                                        TraceId = ByteString.CopyFrom(traceId),
                                                        SpanId = ByteString.CopyFrom(spanId)
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
        var json = OfficialJson(request);
        var point = json["resourceMetrics"]![0]!["scopeMetrics"]![0]!["metrics"]![0]!["histogram"]!["dataPoints"]![0]!;
        var exemplar = point["exemplars"]![0]!;
        exemplar["traceId"] = Convert.ToHexStringLower(traceId);
        exemplar["spanId"] = Convert.ToHexStringLower(spanId);
        point["future"] = new JsonObject
        {
            ["traceId"] = "unrelated-not-an-id",
            ["spanId"] = "also-unrelated"
        };

        var parsed = await OtlpPayloadParser.ParseMetricsRequestAsync(
            NewJsonRequest(json),
            OtlpPayloadEncoding.Json,
            TestContext.Current.CancellationToken);

        var parsedExemplar = Assert.Single(Assert.Single(Assert.Single(
            Assert.Single(Assert.Single(parsed.ResourceMetrics).ScopeMetrics).Metrics).Histogram.DataPoints).Exemplars);
        Assert.Equal(traceId, parsedExemplar.TraceId.ToByteArray());
        Assert.Equal(spanId, parsedExemplar.SpanId.ToByteArray());

        exemplar["traceId"] = "not-hex";
        await Assert.ThrowsAsync<InvalidDataException>(() => OtlpPayloadParser.ParseMetricsRequestAsync(
            NewJsonRequest(json),
            OtlpPayloadEncoding.Json,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Otlp_http_results_emit_official_success_and_error_envelopes()
    {
        var successContext = NewResponseContext();
        await OtlpHttpResult.Success(
                OtlpPayloadEncoding.Protobuf,
                new ExportTraceServiceResponse())
            .ExecuteAsync(successContext);

        Assert.Equal(StatusCodes.Status200OK, successContext.Response.StatusCode);
        Assert.Equal(OtlpPayloadParser.ProtobufContentType, successContext.Response.ContentType);
        var successPayload = ResponseBytes(successContext);
        var success = ExportTraceServiceResponse.Parser.ParseFrom(successPayload);
        Assert.NotNull(success);

        var failureContext = NewResponseContext();
        await OtlpHttpResult.Failure(
                StatusCodes.Status400BadRequest,
                OtlpPayloadEncoding.Protobuf,
                "The OTLP traces payload could not be decoded.")
            .ExecuteAsync(failureContext);

        Assert.Equal(StatusCodes.Status400BadRequest, failureContext.Response.StatusCode);
        Assert.Equal(OtlpPayloadParser.ProtobufContentType, failureContext.Response.ContentType);
        var failure = RpcStatus.Parser.ParseFrom(ResponseBytes(failureContext));
        Assert.Equal("The OTLP traces payload could not be decoded.", failure.Message);
    }

    [Fact]
    public void Public_attribute_projection_preserves_recursive_otlp_values_on_the_generated_json_boundary()
    {
        const string persisted = """
                                 {
                                   "binary":{"type":"bytes","base64":"/wA="},
                                   "empty":null,
                                   "integer":{"type":"int","value":"9223372036854775807"},
                                   "double":{"type":"double","value":"Infinity"},
                                   "kvlist":{"type":"kvlist","values":{"answer":{"type":"int","value":"42"},"nested":[true,"value"]}},
                                   "mixed":[{"type":"int","value":"1"},"two",false,{"type":"kvlist","values":{"deep":[null,{"type":"double","value":3}]}}]
                                 }
                                 """;
        var attributes = Assert.IsAssignableFrom<IReadOnlyList<Qyl.Api.Contracts.Common.Attribute>>(
            ContractJson.ParseAttributes(persisted));

        var node = JsonNode.Parse(JsonSerializer.Serialize(
            attributes.ToArray(),
            QylSerializerContext.Default.AttributeArray))!;
        Assert.Equal("bytes", node[0]!["value"]!["type"]!.GetValue<string>());
        Assert.Equal("/wA=", node[0]!["value"]!["base64"]!.GetValue<string>());
        Assert.True(node[1]!.AsObject().ContainsKey("value"));
        Assert.Null(node[1]!["value"]);
        Assert.Equal("9223372036854775807", node[2]!["value"]!["value"]!.GetValue<string>());
        Assert.Equal("Infinity", node[3]!["value"]!["value"]!.GetValue<string>());
        Assert.Equal("42", node[4]!["value"]!["values"]!["answer"]!["value"]!.GetValue<string>());
        Assert.True(node[4]!["value"]!["values"]!["nested"]![0]!.GetValue<bool>());
        Assert.Equal("two", node[5]!["value"]![1]!.GetValue<string>());
        Assert.Equal(3, node[5]!["value"]![3]!["values"]!["deep"]![1]!["value"]!.GetValue<int>());
    }

    private static DefaultHttpContext NewResponseContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static DefaultHttpContext NewOtlpEndpointContext(string contentType, byte[] payload)
    {
        var context = NewResponseContext();
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.Request.ContentType = contentType;
        context.Request.ContentLength = payload.Length;
        context.Request.Body = new MemoryStream(payload);
        return context;
    }

    private static HttpRequest NewJsonRequest(JsonObject json)
    {
        var payload = Encoding.UTF8.GetBytes(json.ToJsonString());
        var context = new DefaultHttpContext();
        context.Request.ContentType = OtlpPayloadParser.JsonContentType;
        context.Request.ContentLength = payload.Length;
        context.Request.Body = new MemoryStream(payload);
        return context.Request;
    }

    private static JsonObject OfficialJson(IMessage message) =>
        JsonNode.Parse(JsonFormatter.Default.Format(message))!.AsObject();

    private static byte[] SequentialBytes(int start, int length) =>
        Enumerable.Range(start, length).Select(static value => (byte)value).ToArray();

    private static byte[] ResponseBytes(DefaultHttpContext context)
    {
        var stream = Assert.IsType<MemoryStream>(context.Response.Body);
        return stream.ToArray();
    }
}
