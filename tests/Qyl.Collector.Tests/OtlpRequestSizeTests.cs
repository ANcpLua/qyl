using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Google.Protobuf;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Collector.Hosting;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using RpcStatus = Google.Rpc.Status;

namespace Qyl.Collector.Tests;

/// <summary>
/// OTLP 1.11 request size limits: an OTLP/HTTP body over the decompressed bound answers
/// 413 Content Too Large with the OTLP status envelope, in the request's encoding, and never 400.
/// </summary>
[TestClass]
public sealed class OtlpRequestSizeTests(TestContext testContext)
{
    private const int MaxPayloadBytes = 16 * 1024 * 1024;

    [TestMethod]
    public async Task A_declared_oversized_identity_body_answers_413_before_it_is_read()
    {
        await using var store = new DuckDbStore(":memory:");
        var context = NewOtlpEndpointContext(OtlpPayloadParser.ProtobufContentType, new byte[16]);
        context.Request.ContentLength = MaxPayloadBytes + 1L;

        var result = await CollectorEndpointExtensions.IngestOtlpTracesAsync(
            context, store, testContext.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.AreEqual(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.AreEqual(OtlpPayloadParser.ProtobufContentType, context.Response.ContentType);
        var status = RpcStatus.Parser.ParseFrom(ResponseBytes(context));
        Assert.AreEqual(OtlpPayloadParser.PayloadTooLargeMessage, status.Message);
        Assert.IsEmpty(await store.GetSpansAsync(ProjectScope.DefaultProjectId, ct: testContext.CancellationToken));
    }

    [TestMethod]
    public async Task A_gzip_body_that_inflates_past_the_bound_answers_413_in_the_request_encoding()
    {
        await using var store = new DuckDbStore(":memory:");
        var compressed = Gzip(new byte[MaxPayloadBytes + 1]);
        Assert.IsLessThan(MaxPayloadBytes / 64, compressed.Length, $"the bomb should be small on the wire, was {compressed.Length} bytes");
        var context = NewOtlpEndpointContext(OtlpPayloadParser.JsonContentType, compressed);
        context.Request.Headers.ContentEncoding = "gzip";

        var result = await CollectorEndpointExtensions.IngestOtlpLogsAsync(
            context, store, TimeProvider.System, testContext.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.AreEqual(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.AreEqual(OtlpPayloadParser.JsonContentType, context.Response.ContentType);
        var status = JsonParser.Default.Parse<RpcStatus>(Encoding.UTF8.GetString(ResponseBytes(context)));
        Assert.AreEqual(OtlpPayloadParser.PayloadTooLargeMessage, status.Message);
    }

    [TestMethod]
    [DataRow("traces")]
    [DataRow("logs")]
    [DataRow("metrics")]
    public async Task An_unknown_length_gzip_body_is_bounded_on_the_wire_even_when_its_output_is_small(string signal)
    {
        await using var store = new DuckDbStore(":memory:");
        // A valid gzip filename may consume the wire budget without producing any output.
        // The protobuf itself is a valid empty unknown field, so a decoded-only bound accepts it.
        var context = NewOtlpEndpointContext(OtlpPayloadParser.ProtobufContentType, GzipWithOversizedFilename());
        context.Request.ContentLength = null;
        context.Request.Headers.ContentEncoding = "gzip";
        context.Request.Headers.TransferEncoding = "chunked";
        var result = signal switch
        {
            "traces" => await CollectorEndpointExtensions.IngestOtlpTracesAsync(context, store, testContext.CancellationToken),
            "logs" => await CollectorEndpointExtensions.IngestOtlpLogsAsync(context, store, TimeProvider.System, testContext.CancellationToken),
            _ => await CollectorEndpointExtensions.IngestOtlpMetricsAsync(context, store, testContext.CancellationToken)
        };
        await result.ExecuteAsync(context);
        Assert.AreEqual(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.AreEqual(OtlpPayloadParser.ProtobufContentType, context.Response.ContentType);
        Assert.AreEqual(OtlpPayloadParser.PayloadTooLargeMessage, RpcStatus.Parser.ParseFrom(ResponseBytes(context)).Message);
    }

    [TestMethod]
    [DataRow("traces")]
    [DataRow("logs")]
    [DataRow("metrics")]
    public async Task Kestrel_returns_an_OTLP_413_for_a_chunked_gzip_body_over_the_wire_bound(string signal)
    {
        await using var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/{signal}");
        request.Version = HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
        request.Content = new ByteArrayContent(GzipWithOversizedFilename());
        request.Headers.Add(OtlpConstants.ApiKeyHeaderName, "ingester");
        // HttpClient sends chunked framing instead of Content-Length, even though the
        // content can compute its size. The server must enforce a streamed wire bound.
        request.Headers.TransferEncodingChunked = true;
        request.Content.Headers.ContentEncoding.Add("gzip");
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(OtlpPayloadParser.ProtobufContentType);

        using var response = await host.Client.SendAsync(request, testContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.AreEqual(OtlpPayloadParser.ProtobufContentType, response.Content.Headers.ContentType?.MediaType);
        var status = RpcStatus.Parser.ParseFrom(await response.Content.ReadAsByteArrayAsync(testContext.CancellationToken));
        Assert.AreEqual(OtlpPayloadParser.PayloadTooLargeMessage, status.Message);
    }

    [TestMethod]
    public async Task A_body_at_the_bound_is_decoded_not_refused()
    {
        await using var store = new DuckDbStore(":memory:");
        // Exactly the bound of zero bytes: accepted by the size check, then refused as undecodable.
        var context = NewOtlpEndpointContext(OtlpPayloadParser.ProtobufContentType, new byte[MaxPayloadBytes]);

        var result = await CollectorEndpointExtensions.IngestOtlpMetricsAsync(
            context, store, testContext.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.AreEqual(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    private static byte[] Gzip(byte[] payload)
    {
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            gzip.Write(payload);
        return compressed.ToArray();
    }

    private static byte[] GzipWithOversizedFilename()
    {
        var gzip = Gzip([0xfa, 0x07, 0x00]);
        var body = new byte[gzip.Length + MaxPayloadBytes + 2];
        gzip.AsSpan(0, 10).CopyTo(body);
        body[3] |= 0x08; // FNAME: a NUL-terminated filename after the fixed header.
        body.AsSpan(10, MaxPayloadBytes + 1).Fill((byte)'x');
        gzip.AsSpan(10).CopyTo(body.AsSpan(10 + MaxPayloadBytes + 2));
        return body;
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

    private static byte[] ResponseBytes(DefaultHttpContext context) =>
        Assert.IsExactInstanceOfType<MemoryStream>(context.Response.Body).ToArray();
}
