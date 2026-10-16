using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Protobuf;
using Grpc.AspNetCore.Server;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.ApiKeys;
using Qyl.Collector.Grpc;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Mapping;
using Qyl.Collector.Ingestion.Primitives;
using Qyl.Collector.Storage;
using OtlpLogRecord = OpenTelemetry.Proto.Logs.V1.LogRecord;
using OtlpMetric = OpenTelemetry.Proto.Metrics.V1.Metric;
using OtlpResourceMetrics = OpenTelemetry.Proto.Metrics.V1.ResourceMetrics;
using OtlpScopeMetrics = OpenTelemetry.Proto.Metrics.V1.ScopeMetrics;
using OtlpResourceLogs = OpenTelemetry.Proto.Logs.V1.ResourceLogs;
using OtlpScopeLogs = OpenTelemetry.Proto.Logs.V1.ScopeLogs;
using RpcStatus = Google.Rpc.Status;
using GrpcStatus = Grpc.Core.Status;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class OtlpWireTests(TestContext testContext)
{
    [TestMethod]
    [DataRow("application/json", "Json")]
    [DataRow("APPLICATION/JSON; charset=utf-8", "Json")]
    [DataRow("application/x-protobuf", "Protobuf")]
    [DataRow("Application/X-Protobuf; version=1", "Protobuf")]
    public void Otlp_content_type_accepts_only_the_declared_media_types_with_optional_parameters(
        string contentType,
        string expected)
    {
        Assert.AreEqual(expected, OtlpPayloadParser.GetEncoding(contentType).ToString());
    }

    [TestMethod]
    public void Collector_service_graph_registers_the_grpc_api_key_interceptor()
    {
        var services = new ServiceCollection();
        services.AddQylCollectorCore(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<GrpcServiceOptions>>().Value;
        Assert.Contains(registration => registration.Type == typeof(OtlpApiKeyInterceptor),options.Interceptors);
    }

    [TestMethod]
    public async Task Grpc_interceptor_resolves_project_from_key_and_rejects_unknown_credentials()
    {
        var options = new OtlpApiKeyOptions
        {
            AuthMode = "ApiKey",
            Keys = [new ProjectApiKey("alpha", "a1", (ApiKeyRoles.Ingest | ApiKeyRoles.Read))]
        };
        var interceptor = new OtlpApiKeyInterceptor(options);
        var context = new TestServerCallContext(testContext.CancellationToken);
        context.RequestHeaders.Add(OtlpConstants.ApiKeyHeaderName, "a1");

        var project = await interceptor.UnaryServerHandler(
            "request", context,
            (_, call) => Task.FromResult(AuthenticatedProject.ForGrpcIngest(call, options)!));
        Assert.AreEqual("alpha", project);

        var rejected = new TestServerCallContext(testContext.CancellationToken);
        rejected.RequestHeaders.Add(OtlpConstants.ApiKeyHeaderName, "unknown");
        var error = await Assert.ThrowsExactlyAsync<RpcException>(() => interceptor.UnaryServerHandler(
            "request", rejected, (_, _) => Task.FromResult("should not run")));
        Assert.AreEqual(StatusCode.Unauthenticated, error.StatusCode);
    }

    [TestMethod]
    public async Task Grpc_interceptor_refuses_a_key_without_the_ingest_role()
    {
        var options = new OtlpApiKeyOptions
        {
            AuthMode = "ApiKey",
            Keys = [new ProjectApiKey("alpha", "reader", ApiKeyRoles.Read | ApiKeyRoles.Write)]
        };
        var interceptor = new OtlpApiKeyInterceptor(options);
        var context = new TestServerCallContext(testContext.CancellationToken);
        context.RequestHeaders.Add(OtlpConstants.ApiKeyHeaderName, "reader");

        var error = await Assert.ThrowsExactlyAsync<RpcException>(() => interceptor.UnaryServerHandler(
            "request", context, (_, _) => Task.FromResult("should not run")));
        Assert.AreEqual(StatusCode.PermissionDenied, error.StatusCode);
    }

    [TestMethod]
    public async Task Grpc_interceptor_rejects_project_metadata_that_conflicts_with_the_key()
    {
        var options = new OtlpApiKeyOptions
        {
            AuthMode = "ApiKey",
            Keys = [new ProjectApiKey("alpha", "a1", (ApiKeyRoles.Ingest | ApiKeyRoles.Read))]
        };
        var interceptor = new OtlpApiKeyInterceptor(options);

        var conflicting = new TestServerCallContext(testContext.CancellationToken);
        conflicting.RequestHeaders.Add(OtlpConstants.ApiKeyHeaderName, "a1");
        conflicting.RequestHeaders.Add("X-Qyl-Project", "beta");
        var continued = false;
        var error = await Assert.ThrowsExactlyAsync<RpcException>(() => interceptor.UnaryServerHandler(
            "request", conflicting, (_, _) =>
            {
                continued = true;
                return Task.FromResult("should not run");
            }));
        Assert.AreEqual(StatusCode.InvalidArgument, error.StatusCode);
        Assert.IsFalse(continued);

        var matching = new TestServerCallContext(testContext.CancellationToken);
        matching.RequestHeaders.Add(OtlpConstants.ApiKeyHeaderName, "a1");
        matching.RequestHeaders.Add(ProjectIdentity.HeaderName, " alpha ");
        var project = await interceptor.UnaryServerHandler(
            "request", matching,
            (_, call) => Task.FromResult(AuthenticatedProject.ForGrpcIngest(call, options)!));
        Assert.AreEqual("alpha", project);
    }

    [TestMethod]
    public async Task Grpc_export_rejects_a_resource_project_outside_the_credentials_scope()
    {
        var options = new OtlpApiKeyOptions
        {
            AuthMode = "ApiKey",
            Keys = [new ProjectApiKey("alpha", "a1", (ApiKeyRoles.Ingest | ApiKeyRoles.Read))]
        };
        var request = new ExportTraceServiceRequest
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
                                Key = "qyl.project.id",
                                Value = new AnyValue { StringValue = "beta" }
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
                                    TraceId = ByteString.CopyFrom(Enumerable.Range(1, 16).Select(static value => (byte)value).ToArray()),
                                    SpanId = ByteString.CopyFrom(Enumerable.Range(1, 8).Select(static value => (byte)value).ToArray()),
                                    Name = "cross-project",
                                    StartTimeUnixNano = 1,
                                    EndTimeUnixNano = 2
                                }
                            }
                        }
                    }
                }
            }
        };
        var context = new TestServerCallContext(testContext.CancellationToken);
        context.RequestHeaders.Add(OtlpConstants.ApiKeyHeaderName, "a1");
        await using var store = new DuckDbStore(":memory:");

        var error = await Assert.ThrowsExactlyAsync<RpcException>(() =>
            new OtlpApiKeyInterceptor(options).UnaryServerHandler(
                request, context, (message, call) => new TraceServiceImpl(store, options).Export(message, call)));
        Assert.AreEqual(StatusCode.InvalidArgument, error.StatusCode);
        Assert.IsEmpty(await store.GetSpansAsync("alpha", ct: testContext.CancellationToken));
        Assert.IsEmpty(await store.GetSpansAsync("beta", ct: testContext.CancellationToken));
    }

    [TestMethod]
    [DataRow("application/jsonp")]
    [DataRow("application/json-patch+json")]
    [DataRow("application/x-protobufevil")]
    public async Task Otlp_trace_wire_rejects_media_type_prefixes_with_415(string contentType)
    {
        await using var store = new DuckDbStore(":memory:");
        var context = NewOtlpEndpointContext(contentType, new ExportTraceServiceRequest().ToByteArray());
        var result = await CollectorEndpointExtensions.IngestOtlpTracesAsync(
            context,
            store,
            testContext.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.AreEqual(StatusCodes.Status415UnsupportedMediaType, context.Response.StatusCode);
        Assert.AreEqual(OtlpPayloadParser.JsonContentType, context.Response.ContentType);
        var status = JsonParser.Default.Parse<RpcStatus>(
            Encoding.UTF8.GetString(ResponseBytes(context)));
        Assert.AreEqual(
            "Content-Type must be application/x-protobuf or application/json; Content-Encoding must be gzip, identity, or absent.",
            status.Message);
    }

    [TestMethod]
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
        var context = new DefaultHttpContext
        {
            Request =
            {
                ContentType = OtlpPayloadParser.ProtobufContentType,
                ContentLength = payload.Length,
                Body = new MemoryStream(payload)
            }
        };

        var encoding = OtlpPayloadParser.GetEncoding(context.Request.ContentType);
        var parsed = await OtlpPayloadParser.ParseTraceRequestAsync(
            context.Request, encoding, testContext.CancellationToken);
        var batch = OtlpConverter.ConvertTraceRequest(parsed);

        var span = Assert.ContainsSingle(batch.Spans);
        Assert.AreEqual(Convert.ToHexString(traceId).ToLowerInvariant(), span.TraceId);
        Assert.AreEqual(Convert.ToHexString(spanId).ToLowerInvariant(), span.SpanId);
        Assert.AreEqual("wire-test", span.ServiceName);
        Assert.AreEqual("programmatic-operation", span.Name);
        Assert.AreEqual(5100L, span.Attributes["server.port"].AsInt64());
    }

    [TestMethod]
    public async Task Official_protobuf_trace_decodes_from_gzip_with_the_same_bounded_parser()
    {
        var requestMessage = new ExportTraceServiceRequest
        {
            ResourceSpans = { new ResourceSpans() }
        };
        var protobuf = requestMessage.ToByteArray();
        await using var compressed = new MemoryStream();
        await using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            await gzip.WriteAsync(protobuf, testContext.CancellationToken);
        compressed.Position = 0;

        var context = new DefaultHttpContext
        {
            Request =
            {
                ContentType = OtlpPayloadParser.ProtobufContentType,
                Headers =
                {
                    ContentEncoding = "gzip"
                },
                ContentLength = compressed.Length,
                Body = compressed
            }
        };

        var parsed = await OtlpPayloadParser.ParseTraceRequestAsync(
            context.Request,
            OtlpPayloadParser.GetEncoding(context.Request.ContentType),
            testContext.CancellationToken);

        Assert.ContainsSingle(parsed.ResourceSpans);
        Assert.AreEqual(0, compressed.Position);
    }

    [TestMethod]
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
                ["futureId"] = "future-id-is-not-an-otlp-id"
            }
        };
        var payload = Encoding.UTF8.GetBytes(json.ToJsonString());

        var context = new DefaultHttpContext
        {
            Request =
            {
                ContentType = OtlpPayloadParser.JsonContentType,
                ContentLength = payload.Length,
                Body = new MemoryStream(payload)
            }
        };

        var parsed = await OtlpPayloadParser.ParseTraceRequestAsync(
            context.Request,
            OtlpPayloadEncoding.Json,
            testContext.CancellationToken);

        Assert.ContainsSingle(parsed.ResourceSpans);
    }

    [TestMethod]
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
            testContext.CancellationToken);

        var span = Assert.ContainsSingle(Assert.ContainsSingle(Assert.ContainsSingle(parsed.ResourceSpans).ScopeSpans).Spans);
        Assert.AreSequenceEqual(traceId, span.TraceId.ToByteArray());
        Assert.AreSequenceEqual(spanId, span.SpanId.ToByteArray());
        Assert.AreSequenceEqual(parentSpanId, span.ParentSpanId.ToByteArray());
        var link = Assert.ContainsSingle(span.Links);
        Assert.AreSequenceEqual(linkedTraceId, link.TraceId.ToByteArray());
        Assert.AreSequenceEqual(linkedSpanId, link.SpanId.ToByteArray());

        spanJson["traceId"] = "not-hex";
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => OtlpPayloadParser.ParseTraceRequestAsync(
            NewJsonRequest(json),
            OtlpPayloadEncoding.Json,
            testContext.CancellationToken));
    }

    [TestMethod]
    public async Task Otlp_json_normalizes_known_log_ids()
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
            testContext.CancellationToken);
        var log = Assert.ContainsSingle(Assert.ContainsSingle(Assert.ContainsSingle(logs.ResourceLogs).ScopeLogs).LogRecords);
        Assert.AreSequenceEqual(logTraceId, log.TraceId.ToByteArray());
        Assert.AreSequenceEqual(logSpanId, log.SpanId.ToByteArray());
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task Metrics_are_stored_and_untimed_points_reported_as_partial_success_in_both_transports(
        bool json,
        bool gzip)
    {
        const ulong PointTime = 1_700_000_000_000_000_000UL;
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
                                    Name = "wire.gauge",
                                    Unit = "1",
                                    Gauge = new Gauge
                                    {
                                        DataPoints =
                                        {
                                            new NumberDataPoint
                                            {
                                                TimeUnixNano = PointTime, AsDouble = 4
                                            },
                                            new NumberDataPoint
                                            {
                                                TimeUnixNano = PointTime + 1, AsDouble = 6
                                            }
                                        }
                                    }
                                },
                                new OtlpMetric
                                {
                                    Name = "wire.sum",
                                    Sum = new Sum
                                    {
                                        IsMonotonic = true,
                                        AggregationTemporality = AggregationTemporality.Cumulative,
                                        DataPoints =
                                        {
                                            new NumberDataPoint { TimeUnixNano = PointTime, AsInt = 7 }
                                        }
                                    }
                                },
                                new OtlpMetric
                                {
                                    Name = "wire.histogram",
                                    Histogram = new Histogram
                                    {
                                        AggregationTemporality = AggregationTemporality.Delta,
                                        DataPoints =
                                        {
                                            new HistogramDataPoint
                                            {
                                                TimeUnixNano = PointTime,
                                                Count = 3,
                                                Sum = 12,
                                                ExplicitBounds = { 1.0, 5.0 },
                                                BucketCounts = { 1, 1, 1 }
                                            }
                                        }
                                    }
                                },
                                new OtlpMetric
                                {
                                    Name = "wire.exponential-histogram",
                                    ExponentialHistogram = new ExponentialHistogram
                                    {
                                        AggregationTemporality = AggregationTemporality.Delta,
                                        DataPoints =
                                        {
                                            new ExponentialHistogramDataPoint
                                            {
                                                TimeUnixNano = PointTime,
                                                Scale = 0,
                                                Count = 2,
                                                Sum = 6,
                                                Positive = new ExponentialHistogramDataPoint.Types.Buckets
                                                {
                                                    Offset = 1, BucketCounts = { 1, 1 }
                                                }
                                            }
                                        }
                                    }
                                },
                                new OtlpMetric
                                {
                                    Name = "wire.summary",
                                    Summary = new Summary
                                    {
                                        DataPoints =
                                        {
                                            new SummaryDataPoint(),
                                            new SummaryDataPoint(),
                                            new SummaryDataPoint()
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
        var payload = json
            ? Encoding.UTF8.GetBytes(JsonFormatter.Default.Format(request))
            : request.ToByteArray();
        if (gzip)
            payload = Gzip(payload);

        var context = NewOtlpEndpointContext(
            json ? OtlpPayloadParser.JsonContentType : OtlpPayloadParser.ProtobufContentType,
            payload);
        if (gzip)
            context.Request.Headers.ContentEncoding = "gzip";

        await using var store = new DuckDbStore(":memory:");
        var result = await CollectorEndpointExtensions.IngestOtlpMetricsAsync(
            context,
            store,
            testContext.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.AreEqual(
            json ? OtlpPayloadParser.JsonContentType : OtlpPayloadParser.ProtobufContentType,
            context.Response.ContentType);
        var response = json
            ? JsonParser.Default.Parse<ExportMetricsServiceResponse>(
                Encoding.UTF8.GetString(ResponseBytes(context)))
            : ExportMetricsServiceResponse.Parser.ParseFrom(ResponseBytes(context));
        AssertUntimedPointsRejected(response);

        // Every timed shape in the request reached storage through the HTTP path.
        var catalog = await store.ListMetricsAsync(
            ProjectScope.DefaultProjectId,
            ct: testContext.CancellationToken);
        Assert.AreSequenceEqual(
            ["wire.exponential-histogram", "wire.gauge", "wire.histogram", "wire.sum"],
            catalog.Select(static entry => entry.MetricName));
        Assert.DoesNotContain(static entry => entry.MetricName is "wire.summary",catalog);

        await using var grpcStore = new DuckDbStore(":memory:");
        var grpcResponse = await new MetricsServiceImpl(grpcStore).Export(
            request,
            new TestServerCallContext(testContext.CancellationToken));
        AssertUntimedPointsRejected(grpcResponse);
        Assert.AreSequenceEqual(
            catalog.Select(static entry => entry.MetricName),
            (await grpcStore.ListMetricsAsync(
                ProjectScope.DefaultProjectId,
                ct: testContext.CancellationToken))
            .Select(static entry => entry.MetricName));
    }

    [TestMethod]
    public async Task Otlp_http_results_emit_official_success_and_error_envelopes()
    {
        var successContext = NewResponseContext();
        await OtlpHttpResult.Success(
                OtlpPayloadEncoding.Protobuf,
                new ExportTraceServiceResponse())
            .ExecuteAsync(successContext);

        Assert.AreEqual(StatusCodes.Status200OK, successContext.Response.StatusCode);
        Assert.AreEqual(OtlpPayloadParser.ProtobufContentType, successContext.Response.ContentType);
        var successPayload = ResponseBytes(successContext);
        var success = ExportTraceServiceResponse.Parser.ParseFrom(successPayload);
        Assert.IsNotNull(success);

        var failureContext = NewResponseContext();
        await OtlpHttpResult.Failure(
                StatusCodes.Status400BadRequest,
                OtlpPayloadEncoding.Protobuf,
                "The OTLP traces payload could not be decoded.")
            .ExecuteAsync(failureContext);

        Assert.AreEqual(StatusCodes.Status400BadRequest, failureContext.Response.StatusCode);
        Assert.AreEqual(OtlpPayloadParser.ProtobufContentType, failureContext.Response.ContentType);
        var failure = RpcStatus.Parser.ParseFrom(ResponseBytes(failureContext));
        Assert.AreEqual("The OTLP traces payload could not be decoded.", failure.Message);
    }

    [TestMethod]
    public void Public_attribute_projection_preserves_recursive_otlp_values_on_the_generated_json_boundary()
    {
        const string Persisted = """
                                 {
                                   "binary":{"type":"bytes","base64":"/wA="},
                                   "empty":null,
                                   "integer":{"type":"int","value":"9223372036854775807"},
                                   "double":{"type":"double","value":"Infinity"},
                                   "kvlist":{"type":"kvlist","values":{"answer":{"type":"int","value":"42"},"nested":[true,"value"]}},
                                   "mixed":[{"type":"int","value":"1"},"two",false,{"type":"kvlist","values":{"deep":[null,{"type":"double","value":3}]}}]
                                 }
                                 """;
        var attributes = Assert.IsInstanceOfType<IReadOnlyList<Qyl.Api.Contracts.Common.Attribute>>(
            ContractJson.ParseAttributes(Persisted));

        var node = JsonNode.Parse(JsonSerializer.Serialize(
            attributes.ToArray(),
            QylSerializerContext.Default.AttributeArray))!;
        Assert.AreEqual("bytes", node[0]!["value"]!["type"]!.GetValue<string>());
        Assert.AreEqual("/wA=", node[0]!["value"]!["base64"]!.GetValue<string>());
        Assert.IsTrue(node[1]!.AsObject().ContainsKey("value"));
        Assert.IsNull(node[1]!["value"]);
        Assert.AreEqual("9223372036854775807", node[2]!["value"]!["value"]!.GetValue<string>());
        Assert.AreEqual("Infinity", node[3]!["value"]!["value"]!.GetValue<string>());
        Assert.AreEqual("42", node[4]!["value"]!["values"]!["answer"]!["value"]!.GetValue<string>());
        Assert.IsTrue(node[4]!["value"]!["values"]!["nested"]![0]!.GetValue<bool>());
        Assert.AreEqual("two", node[5]!["value"]![1]!.GetValue<string>());
        Assert.AreEqual(3, node[5]!["value"]![3]!["values"]!["deep"]![1]!["value"]!.GetValue<int>());
    }

    /// <summary>
    /// The smallest ServerCallContext the OTLP service implementations actually read: they
    /// use only the cancellation token, and gRPC ships no in-box test double for ASP.NET.
    /// </summary>
    private sealed class TestServerCallContext(CancellationToken cancellationToken)
        : ServerCallContext
    {
        protected override string MethodCore => "/test/Export";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "ipv4:127.0.0.1:0";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore { get; } = [];
        protected override CancellationToken CancellationTokenCore { get; } = cancellationToken;
        protected override Metadata ResponseTrailersCore { get; } = [];
        protected override GrpcStatus StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore { get; } = new("", new Dictionary<string, List<AuthProperty>>());
        protected override IDictionary<object, object> UserStateCore { get; } = new Dictionary<object, object>();

        protected override ContextPropagationToken CreatePropagationTokenCore(
            ContextPropagationOptions? options) =>
            throw new NotSupportedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) =>
            Task.CompletedTask;
    }

    private static DefaultHttpContext NewResponseContext()
    {
        return new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }

    // The three untimed points cannot be placed on a metric timeline. Other points remain stored.
    private static void AssertUntimedPointsRejected(ExportMetricsServiceResponse response)
    {
        Assert.IsNotNull(response.PartialSuccess);
        Assert.AreEqual(3, response.PartialSuccess.RejectedDataPoints);
        Assert.Contains("time_unix_nano", response.PartialSuccess.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("wire.summary", response.PartialSuccess.ErrorMessage, StringComparison.Ordinal);
    }

    private static byte[] Gzip(byte[] payload)
    {
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            gzip.Write(payload);
        return compressed.ToArray();
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
        var context = new DefaultHttpContext
        {
            Request =
            {
                ContentType = OtlpPayloadParser.JsonContentType,
                ContentLength = payload.Length,
                Body = new MemoryStream(payload)
            }
        };
        return context.Request;
    }

    private static JsonObject OfficialJson(IMessage message) =>
        JsonNode.Parse(JsonFormatter.Default.Format(message))!.AsObject();

    private static byte[] SequentialBytes(int start, int length) =>
        Enumerable.Range(start, length).Select(static value => (byte)value).ToArray();

    private static byte[] ResponseBytes(DefaultHttpContext context)
    {
        var stream = Assert.IsExactInstanceOfType<MemoryStream>(context.Response.Body);
        return stream.ToArray();
    }
}
