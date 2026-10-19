using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

/// <summary>
/// OTLP 1.11.1 "UTF-8 String Handling": a decoder SHOULD replace an invalid UTF-8 sequence with
/// U+FFFD instead of refusing the export. One regression per ingestion path; before the change
/// each path rejected the whole export as undecodable.
/// </summary>
[TestClass]
public sealed class OtlpUtf8Tests(TestContext testContext)
{
    // Serialized with this ASCII byte in place, then patched to 0xFF on the wire: a C# string
    // cannot carry an invalid UTF-8 sequence, so the bytes are corrupted after encoding.
    private const char Placeholder = '\u007F';
    private const string SpanName = "span\u007Fname";
    private const string Route = "/orders/\u007F";

    [TestMethod]
    public async Task Otlp_http_protobuf_decodes_invalid_utf8_as_replacement_characters()
    {
        await using var store = new DuckDbStore(":memory:");
        var context = NewOtlpEndpointContext(OtlpPayloadParser.ProtobufContentType, CorruptedTracePayload());

        var result = await CollectorEndpointExtensions.IngestOtlpTracesAsync(
            context, store, testContext.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        AssertReplaced(Assert.ContainsSingle(await store.GetSpansAsync(ProjectScope.DefaultProjectId, ct: testContext.CancellationToken)));
    }

    [TestMethod]
    public async Task Otlp_grpc_decodes_invalid_utf8_as_replacement_characters()
    {
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);
        using var channel = GrpcChannel.ForAddress(host.GrpcAddress);
        var headers = new Metadata { { OtlpConstants.ApiKeyHeaderName, "ingester" } };

        var response = await channel.CreateCallInvoker().AsyncUnaryCall(
            RawTraceExport(),
            null,
            new CallOptions(headers, cancellationToken: testContext.CancellationToken),
            CorruptedTracePayload());

        Assert.IsNull(response.PartialSuccess);
        AssertReplaced(Assert.ContainsSingle(await host.Store.GetSpansAsync("alpha", ct: testContext.CancellationToken)));
    }

    [TestMethod]
    public async Task Otlp_json_decodes_invalid_utf8_and_unpaired_surrogate_escapes_as_replacement_characters()
    {
        // An unpaired high-surrogate escape in the span name and a raw invalid byte in the route.
        var json =
            "{\"resourceSpans\":[{\"resource\":{\"attributes\":[{\"key\":\"service.name\",\"value\":{\"stringValue\":\"utf8-test\"}}]}," +
            "\"scopeSpans\":[{\"spans\":[{\"traceId\":\"000102030405060708090a0b0c0d0e0f\",\"spanId\":\"1011121314151617\"," +
            "\"name\":\"span\\ud800name\",\"kind\":2,\"startTimeUnixNano\":\"1000\",\"endTimeUnixNano\":\"2000\"," +
            "\"attributes\":[{\"key\":\"http.route\",\"value\":{\"stringValue\":\"" + Route + "\"}}]}]}]}]}";
        var payload = Corrupt(Encoding.UTF8.GetBytes(json), Route);
        await using var store = new DuckDbStore(":memory:");
        var context = NewOtlpEndpointContext(OtlpPayloadParser.JsonContentType, payload);

        var result = await CollectorEndpointExtensions.IngestOtlpTracesAsync(
            context, store, testContext.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        AssertReplaced(Assert.ContainsSingle(await store.GetSpansAsync(ProjectScope.DefaultProjectId, ct: testContext.CancellationToken)));
    }

    private static void AssertReplaced(SpanStorageRow span)
    {
        Assert.AreEqual("span�name", span.Name);
        using var attributes = JsonDocument.Parse(span.AttributesJson!);
        Assert.AreEqual("/orders/�", attributes.RootElement.GetProperty("http.route").GetString());
    }

    private static byte[] CorruptedTracePayload() =>
        Corrupt(Corrupt(TraceRequest().ToByteArray(), SpanName), Route);

    private static byte[] Corrupt(byte[] payload, string text)
    {
        var at = payload.AsSpan().IndexOf(Encoding.UTF8.GetBytes(text));
        Assert.IsTrue(at >= 0, $"'{text}' must be serialized verbatim");
        payload[at + text.IndexOf(Placeholder, StringComparison.Ordinal)] = 0xFF;
        return payload;
    }

    private static ExportTraceServiceRequest TraceRequest() => new()
    {
        ResourceSpans =
        {
            new ResourceSpans
            {
                Resource = new Resource
                {
                    Attributes = { new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "utf8-test" } } }
                },
                ScopeSpans =
                {
                    new ScopeSpans
                    {
                        Spans =
                        {
                            new Span
                            {
                                TraceId = ByteString.CopyFrom(Enumerable.Range(0, 16).Select(static value => (byte)value).ToArray()),
                                SpanId = ByteString.CopyFrom(Enumerable.Range(16, 8).Select(static value => (byte)value).ToArray()),
                                Name = SpanName,
                                Kind = Span.Types.SpanKind.Server,
                                StartTimeUnixNano = 1_000,
                                EndTimeUnixNano = 2_000,
                                Attributes = { new KeyValue { Key = "http.route", Value = new AnyValue { StringValue = Route } } }
                            }
                        }
                    }
                }
            }
        }
    };

    // The request travels as the bytes given: a client marshaller that serialized the generated
    // message would refuse the invalid sequence before it reached the wire.
    private static Method<byte[], ExportTraceServiceResponse> RawTraceExport()
    {
        var service = TraceServiceReflection.Descriptor.Services[0];
        return new Method<byte[], ExportTraceServiceResponse>(
            MethodType.Unary,
            service.FullName,
            service.Methods[0].Name,
            Marshallers.Create<byte[]>(static (bytes, context) => context.Complete(bytes), static context => context.PayloadAsNewBuffer()),
            Marshallers.Create(
                static (message, context) => context.Complete(message.ToByteArray()),
                static context => ExportTraceServiceResponse.Parser.ParseFrom(context.PayloadAsReadOnlySequence())));
    }

    private static DefaultHttpContext NewOtlpEndpointContext(string contentType, byte[] payload)
    {
        return new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            },
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            Request =
            {
                ContentType = contentType,
                ContentLength = payload.Length,
                Body = new MemoryStream(payload)
            }
        };
    }
}
