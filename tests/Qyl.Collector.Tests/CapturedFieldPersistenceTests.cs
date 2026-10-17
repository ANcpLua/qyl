using System.Net;
using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Collector.Ingestion;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class CapturedFieldPersistenceTests(TestContext testContext)
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Selected_arrays_survive_ingestion_reopen_and_api_while_credentials_are_dropped(bool grpc)
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-captured-fields-{Guid.NewGuid():N}.duckdb");
        var traceId = ByteString.CopyFrom([.. new byte[15], 1]);
        var span = new Span
        {
            TraceId = traceId, SpanId = ByteString.CopyFrom([.. new byte[7], 1]), Name = "captured-fields",
            StartTimeUnixNano = 1_800_000_000_000_000_000, EndTimeUnixNano = 1_800_000_000_000_000_001
        };
        string[] benign = ["http.request.header.x-request-id", "http.request.header.x-message-id",
            "http.response.header.content-type", "http.response.header.x-ratelimit-remaining",
            "rpc.request.metadata.x-tenant-id", "rpc.response.metadata.x-demo-res-md"];
        string[] denied = ["http.request.header.authorization", "http.response.header.set-cookie",
            "rpc.request.metadata.x-api-key", "rpc.response.metadata.x-auth-token", "HTTP.REQUEST.HEADER.X-JWT",
            "rpc.response.metadata.x-privatekey", "http.request.header", "rpc.request.metadata.",
            "http.request.header.bad name", "custom.message", "baggage.id"];
        for (var i = 0; i < benign.Length; i++)
            span.Attributes.Add(Array(benign[i], i % 2 == 0 ? ["one", "two", "two", ""] : ["single"]));
        foreach (var key in denied) span.Attributes.Add(Array(key, ["opaque-never-retained"]));
        var request = new ExportTraceServiceRequest
        {
            ResourceSpans = { new ResourceSpans { ScopeSpans = { new ScopeSpans { Spans = { span } } } } }
        };
        var decoded = Assert.ContainsSingle(OtlpConverter.ConvertTraceRequest(request).Spans).Attributes;
        foreach (var key in benign) Assert.Contains(key, decoded.Keys);
        foreach (var key in denied) Assert.DoesNotContain(key, decoded.Keys);
        try
        {
            await using (var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken, path))
            {
                ExportTraceServiceResponse ack;
                if (grpc)
                {
                    using var channel = GrpcChannel.ForAddress(host.GrpcAddress);
                    var method = new Method<ExportTraceServiceRequest, ExportTraceServiceResponse>(MethodType.Unary,
                        "opentelemetry.proto.collector.trace.v1.TraceService", "Export",
                        Marshallers.Create(static value => value.ToByteArray(), ExportTraceServiceRequest.Parser.ParseFrom),
                        Marshallers.Create(static value => value.ToByteArray(), ExportTraceServiceResponse.Parser.ParseFrom));
                    using var call = channel.CreateCallInvoker().AsyncUnaryCall(method, null,
                        new CallOptions(new Metadata { { OtlpConstants.ApiKeyHeaderName, "ingester" } }, cancellationToken: testContext.CancellationToken), request);
                    ack = await call.ResponseAsync;
                }
                else
                {
                    using var response = await host.SendProtobufAsync("/v1/traces", request, "ingester");
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    ack = ExportTraceServiceResponse.Parser.ParseFrom(await response.Content.ReadAsByteArrayAsync(testContext.CancellationToken));
                }
                Assert.AreEqual(0L, ack.PartialSuccess?.RejectedSpans ?? 0);
            }
            await using var reopened = await CollectorApiTestHost.StartAsync(testContext.CancellationToken, path);
            var id = Convert.ToHexStringLower(traceId.Span);
            var row = Assert.ContainsSingle(await reopened.Store.GetTraceAsync(id, "alpha", testContext.CancellationToken));
            using var stored = JsonDocument.Parse(row.AttributesJson!);
            using var apiResponse = await reopened.SendAsync(HttpMethod.Get, $"/api/v1/traces/{id}/spans", "reader");
            Assert.AreEqual(HttpStatusCode.OK, apiResponse.StatusCode);
            var page = await reopened.JsonAsync(apiResponse);
            var attributes = Assert.ContainsSingle(page.GetProperty("items").EnumerateArray()).GetProperty("attributes")
                .EnumerateArray().ToDictionary(static item => item.GetProperty("key").GetString()!, static item => item.GetProperty("value"));
            for (var i = 0; i < benign.Length; i++)
            {
                string[] expected = i % 2 == 0 ? ["one", "two", "two", ""] : ["single"];
                Assert.AreSequenceEqual(expected, stored.RootElement.GetProperty(benign[i]).EnumerateArray().Select(static item => item.GetString()!));
                Assert.AreSequenceEqual(expected, attributes[benign[i]].EnumerateArray().Select(static item => item.GetString()!));
            }
            foreach (var key in denied)
            {
                Assert.IsFalse(stored.RootElement.TryGetProperty(key, out _), key);
                Assert.DoesNotContain(key, attributes.Keys);
            }
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".wal");
        }
    }

    private static KeyValue Array(string key, string[] values) => new()
    {
        Key = key, Value = new AnyValue { ArrayValue = new ArrayValue { Values = { values.Select(static value => new AnyValue { StringValue = value }) } } }
    };
}
