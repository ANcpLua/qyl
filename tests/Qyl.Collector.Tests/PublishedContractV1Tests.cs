using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Api.Contracts.Streaming;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using OtlpEntityRef = OpenTelemetry.Proto.Common.V1.EntityRef;
using OtlpLogRecord = OpenTelemetry.Proto.Logs.V1.LogRecord;
using OtlpResource = OpenTelemetry.Proto.Resource.V1.Resource;
using OtlpResourceLogs = OpenTelemetry.Proto.Logs.V1.ResourceLogs;
using OtlpScopeLogs = OpenTelemetry.Proto.Logs.V1.ScopeLogs;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class PublishedContractV1Tests(TestContext testContext)
{
    [TestMethod]
    public async Task Official_otlp_traces_and_logs_emit_lossless_v1_rest_and_sse_json()
    {
        var cancellationToken = testContext.CancellationToken;
        await using var store = new DuckDbStore(":memory:");

        var traceRequest = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    SchemaUrl = "https://opentelemetry.io/schemas/1.39.0",
                    Resource = BuildResource(),
                    ScopeSpans =
                    {
                        new ScopeSpans
                        {
                            Spans =
                            {
                                new OtlpSpan
                                {
                                    TraceId = ByteString.CopyFrom([.. Enumerable.Repeat((byte)1, 16)]),
                                    SpanId = ByteString.CopyFrom([.. Enumerable.Repeat((byte)2, 8)]),
                                    Name = "published-contract-trace",
                                    Kind = OtlpSpan.Types.SpanKind.Server,
                                    StartTimeUnixNano = 1_000,
                                    EndTimeUnixNano = 2_000,
                                    Attributes =
                                    {
                                        Attribute("server.port", new AnyValue { IntValue = long.MaxValue }),
                                        Attribute(
                                            "http.response.status_code",
                                            new AnyValue { DoubleValue = double.PositiveInfinity }),
                                        Attribute(
                                            "http.request.method",
                                            new AnyValue
                                            {
                                                KvlistValue = new KeyValueList
                                                {
                                                    Values =
                                                    {
                                                        new KeyValue
                                                        {
                                                            Key = "nested",
                                                            Value = new AnyValue { BoolValue = true }
                                                        }
                                                    }
                                                }
                                            }),
                                        Attribute("error.type", new AnyValue()),
                                        Attribute(
                                            "network.type",
                                            new AnyValue
                                            {
                                                ArrayValue = new ArrayValue
                                                {
                                                    Values =
                                                    {
                                                        new AnyValue { DoubleValue = double.NegativeInfinity },
                                                        new AnyValue { IntValue = long.MinValue }
                                                    }
                                                }
                                            }),
                                        Attribute(
                                            "server.address",
                                            new AnyValue { BytesValue = ByteString.CopyFrom(4, 5, 6) })
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
        await store.EnqueueAsync(
            new SpanBatch(IngestionStorageMapper.ToSpanStorageRows(OtlpConverter.ConvertTraceRequest(traceRequest))),
            cancellationToken);

        var logsRequest = new ExportLogsServiceRequest
        {
            ResourceLogs =
            {
                new OtlpResourceLogs
                {
                    Resource = BuildResource(),
                    ScopeLogs =
                    {
                        new OtlpScopeLogs
                        {
                            LogRecords =
                            {
                                new OtlpLogRecord
                                {
                                    TimeUnixNano = 3_000,
                                    ObservedTimeUnixNano = 3_001,
                                    SeverityNumber = SeverityNumber.Info,
                                    SeverityText = "INFO",
                                    Body = new AnyValue { StringValue = "published-contract-log" },
                                    Attributes =
                                    {
                                        Attribute("error.type", new AnyValue { IntValue = long.MinValue })
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
        await store.InsertLogsAsync(
            IngestionStorageMapper.ToLogStorageRows(OtlpConverter.ConvertLogs(logsRequest,
                new FakeTimeProvider(CollectorApiTestHost.FixtureUtcNow))),
            cancellationToken);

        var traceJson = await ExecuteAsync(
            static (context, state, ct) => CollectorEndpointExtensions.GetTracesAsync(context, state, ct),
            store,
            cancellationToken);
        var logJson = await ExecuteAsync(
            static (context, state, ct) => CollectorEndpointExtensions.GetLogsAsync(
                context, state, null, null, null, null, null, ct),
            store,
            cancellationToken);
        Assert.IsNotNull(JsonSerializer.Deserialize(traceJson, global::Qyl.Api.Contracts.ContractTypes.TypeInfo<global::Qyl.Api.Contracts.Common.Pagination.CursorPageTrace>()));
        Assert.IsNotNull(JsonSerializer.Deserialize(logJson, global::Qyl.Api.Contracts.ContractTypes.TypeInfo<global::Qyl.Api.Contracts.Common.Pagination.CursorPageLogRecord>()));

        var traceNode = JsonNode.Parse(traceJson)!;
        var logNode = JsonNode.Parse(logJson)!;
        var spanNode = traceNode["items"]![0]!["spans"]![0]!;
        var logRecordNode = logNode["items"]![0]!;
        AssertPublishedResource(spanNode["resource"]!);
        AssertPublishedResource(logRecordNode["resource"]!);

        var spanAttributes = ReadAttributes(spanNode["attributes"]!);
        Assert.AreEqual(long.MaxValue.ToString(CultureInfo.InvariantCulture),
            spanAttributes["server.port"]["value"]!["value"]!.GetValue<string>());
        Assert.AreEqual("Infinity",
            spanAttributes["http.response.status_code"]["value"]!["value"]!.GetValue<string>());
        Assert.IsTrue(spanAttributes["http.request.method"]["value"]!["values"]!["nested"]!.GetValue<bool>());
        Assert.IsTrue(spanAttributes["error.type"].ContainsKey("value"));
        Assert.IsNull(spanAttributes["error.type"]["value"]);
        Assert.AreEqual("-Infinity", spanAttributes["network.type"]["value"]![0]!["value"]!.GetValue<string>());
        Assert.AreEqual("BAUG", spanAttributes["server.address"]["value"]!["base64"]!.GetValue<string>());
        AssertV1Attributes(spanAttributes.Values);

        var logAttributes = ReadAttributes(logRecordNode["attributes"]!);
        Assert.AreEqual(long.MinValue.ToString(CultureInfo.InvariantCulture),
            logAttributes["error.type"]["value"]!["value"]!.GetValue<string>());
        AssertV1Attributes(logAttributes.Values);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await using var stream = CollectorEndpointExtensions.StreamLogEventsAsync(
                store,
                new FakeTimeProvider(),
                "default",
                serviceName: null,
                minSeverity: null,
                query: null,
                afterIngestSequence: null,
                timeout.Token)
            .GetAsyncEnumerator(timeout.Token);
        Assert.IsTrue(await stream.MoveNextAsync());
        var streamEvent = Assert.IsExactInstanceOfType<LogStreamEvent>(stream.Current.Log);
        var streamJson = JsonSerializer.Serialize(streamEvent, global::Qyl.Api.Contracts.ContractTypes.TypeInfo<global::Qyl.Api.Contracts.Streaming.LogStreamEvent>());
        Assert.IsNotNull(JsonSerializer.Deserialize(streamJson, global::Qyl.Api.Contracts.ContractTypes.TypeInfo<global::Qyl.Api.Contracts.Streaming.LogStreamEvent>()));
        AssertPublishedResource(JsonNode.Parse(streamJson)!["data"]!["resource"]!);
    }

    private static OtlpResource BuildResource()
    {
        var array = new ArrayValue
        {
            Values =
            {
                new AnyValue(),
                new AnyValue { IntValue = long.MinValue },
                new AnyValue { DoubleValue = double.NegativeInfinity },
                new AnyValue
                {
                    KvlistValue = new KeyValueList
                    {
                        Values =
                        {
                            new KeyValue { Key = "nested", Value = new AnyValue { BoolValue = true } }
                        }
                    }
                }
            }
        };
        var keyValueList = new KeyValueList
        {
            Values =
            {
                new KeyValue { Key = "answer", Value = new AnyValue { DoubleValue = 3.5 } },
                new KeyValue
                {
                    Key = "nested",
                    Value = new AnyValue
                    {
                        ArrayValue = new ArrayValue
                        {
                            Values =
                            {
                                new AnyValue { StringValue = "value" },
                                new AnyValue()
                            }
                        }
                    }
                }
            }
        };

        return new OtlpResource
        {
            Attributes =
            {
                Attribute("service.name", new AnyValue { StringValue = "published-contract" }),
                Attribute("service.namespace", new AnyValue()),
                Attribute("service.version", new AnyValue { IntValue = long.MaxValue }),
                Attribute("os.version", new AnyValue { DoubleValue = double.PositiveInfinity }),
                Attribute("deployment.environment.name", new AnyValue { ArrayValue = array }),
                Attribute("os.description", new AnyValue { KvlistValue = keyValueList }),
                Attribute("os.name", new AnyValue { BytesValue = ByteString.CopyFrom(0, 1, 255) }),
                Attribute("os.type", new AnyValue { BoolValue = true })
            },
            EntityRefs =
            {
                new OtlpEntityRef
                {
                    SchemaUrl = "https://opentelemetry.io/schemas/1.39.0",
                    Type = "service",
                    IdKeys = { "service.name" },
                    DescriptionKeys = { "service.version" }
                }
            }
        };
    }

    private static KeyValue Attribute(string key, AnyValue value) => new() { Key = key, Value = value };

    private static async Task<string> ExecuteAsync(
        Func<DefaultHttpContext, IQylStore, CancellationToken, Task<IResult>> endpoint,
        IQylStore store,
        CancellationToken cancellationToken)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .ConfigureHttpJsonOptions(static options =>
                    options.SerializerOptions.TypeInfoResolverChain.Insert(0, QylSerializerContext.Default))
                .BuildServiceProvider(),
            Response =
            {
                Body = new MemoryStream()
            }
        };
        var result = await endpoint(context, store, cancellationToken);
        await result.ExecuteAsync(context);
        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static void AssertPublishedResource(JsonNode resourceNode)
    {
        var resource = resourceNode.AsObject();
        var attributes = ReadAttributes(resource["attributes"]!);
        Assert.AreEqual("published-contract", attributes["service.name"]["value"]!.GetValue<string>());

        var empty = attributes["service.namespace"];
        Assert.IsTrue(empty.ContainsKey("value"));
        Assert.IsNull(empty["value"]);

        var integer = attributes["service.version"]["value"]!.AsObject();
        Assert.AreEqual("int", integer["type"]!.GetValue<string>());
        Assert.AreEqual(long.MaxValue.ToString(CultureInfo.InvariantCulture), integer["value"]!.GetValue<string>());

        var infinity = attributes["os.version"]["value"]!.AsObject();
        Assert.AreEqual("double", infinity["type"]!.GetValue<string>());
        Assert.AreEqual("Infinity", infinity["value"]!.GetValue<string>());

        var array = attributes["deployment.environment.name"]["value"]!.AsArray();
        Assert.IsNull(array[0]);
        Assert.AreEqual(long.MinValue.ToString(CultureInfo.InvariantCulture), array[1]!["value"]!.GetValue<string>());
        Assert.AreEqual("-Infinity", array[2]!["value"]!.GetValue<string>());
        Assert.IsTrue(array[3]!["values"]!["nested"]!.GetValue<bool>());

        var keyValueList = attributes["os.description"]["value"]!.AsObject();
        Assert.AreEqual("kvlist", keyValueList["type"]!.GetValue<string>());
        Assert.AreEqual(3.5, keyValueList["values"]!["answer"]!["value"]!.GetValue<double>());
        Assert.AreEqual("value", keyValueList["values"]!["nested"]![0]!.GetValue<string>());
        Assert.IsNull(keyValueList["values"]!["nested"]![1]);

        var bytes = attributes["os.name"]["value"]!.AsObject();
        Assert.AreEqual("bytes", bytes["type"]!.GetValue<string>());
        Assert.AreEqual("AAH/", bytes["base64"]!.GetValue<string>());
        Assert.IsTrue(attributes["os.type"]["value"]!.GetValue<bool>());

        AssertV1Attributes(attributes.Values);

        var entityRef = Assert.ContainsSingle(resource["entity_refs"]!.AsArray())!.AsObject();
        Assert.AreEqual("https://opentelemetry.io/schemas/1.39.0", entityRef["schema_url"]!.GetValue<string>());
        Assert.AreEqual("service", entityRef["type"]!.GetValue<string>());
        Assert.AreEqual("service.name", Assert.ContainsSingle(entityRef["id_keys"]!.AsArray())!.GetValue<string>());
        Assert.AreEqual(
            "service.version",
            Assert.ContainsSingle(entityRef["description_keys"]!.AsArray())!.GetValue<string>());
    }

    private static Dictionary<string, JsonObject> ReadAttributes(JsonNode attributes) =>
        attributes.AsArray().ToDictionary(
            static item => item!["key"]!.GetValue<string>(),
            static item => item!.AsObject(),
            StringComparer.Ordinal);

    private static void AssertV1Attributes(IEnumerable<JsonObject> attributes)
    {
        foreach (var attribute in attributes)
            AssertV1AttributeValue(attribute["value"]);
    }

    private static void AssertV1AttributeValue(JsonNode? value)
    {
        if (value is null)
            return;

        switch (value.GetValueKind())
        {
            case JsonValueKind.String:
            case JsonValueKind.True:
            case JsonValueKind.False:
                return;
            case JsonValueKind.Array:
                foreach (var item in value.AsArray())
                    AssertV1AttributeValue(item);
                return;
            case JsonValueKind.Number:
                Assert.Fail("Raw JSON numbers are not a lossless OTLP AttributeValue projection.");
                return;
            case JsonValueKind.Object:
                break;
            default:
                Assert.Fail($"Unexpected AttributeValue JSON kind: {value.GetValueKind()}.");
                return;
        }

        var wrapper = value.AsObject();
        var type = wrapper["type"]?.GetValue<string>();
        switch (type)
        {
            case "bytes":
                AssertExactProperties(wrapper, "type", "base64");
                Assert.IsNotNull(Convert.FromBase64String(wrapper["base64"]!.GetValue<string>()));
                return;
            case "int":
                AssertExactProperties(wrapper, "type", "value");
                Assert.IsTrue(long.TryParse(
                    wrapper["value"]!.GetValue<string>(),
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out _));
                return;
            case "double":
                AssertExactProperties(wrapper, "type", "value");
                var doubleValue = wrapper["value"]!;
                Assert.IsTrue(
                    doubleValue.GetValueKind() is JsonValueKind.Number ||
                    doubleValue.GetValue<string>() is "NaN" or "Infinity" or "-Infinity");
                return;
            case "kvlist":
                AssertExactProperties(wrapper, "type", "values");
                foreach (var property in wrapper["values"]!.AsObject())
                    AssertV1AttributeValue(property.Value);
                return;
            default:
                Assert.Fail($"Unknown tagged AttributeValue type: {type ?? "<missing>"}.");
                return;
        }
    }

    private static void AssertExactProperties(JsonObject value, params string[] expected)
    {
        Assert.AreEqual(expected.Length, value.Count);
        foreach (var property in expected)
            Assert.IsTrue(value.ContainsKey(property), $"Missing required property '{property}'.");
    }
}
