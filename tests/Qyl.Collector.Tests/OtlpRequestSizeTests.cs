using System.IO.Compression;
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
public sealed class OtlpRequestSizeTests
{
    private const int MaxPayloadBytes = 16 * 1024 * 1024;

    [Fact]
    public async Task A_declared_oversized_identity_body_answers_413_before_it_is_read()
    {
        await using var store = new DuckDbStore(":memory:");
        var context = NewOtlpEndpointContext(OtlpPayloadParser.ProtobufContentType, new byte[16]);
        context.Request.ContentLength = MaxPayloadBytes + 1L;

        var result = await CollectorEndpointExtensions.IngestOtlpTracesAsync(
            context, store, TestContext.Current.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.Equal(OtlpPayloadParser.ProtobufContentType, context.Response.ContentType);
        var status = RpcStatus.Parser.ParseFrom(ResponseBytes(context));
        Assert.Equal(OtlpPayloadParser.PayloadTooLargeMessage, status.Message);
        Assert.Empty(await store.GetSpansAsync(Qyl.Collector.Storage.ProjectScope.DefaultProjectId, ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_gzip_body_that_inflates_past_the_bound_answers_413_in_the_request_encoding()
    {
        await using var store = new DuckDbStore(":memory:");
        var compressed = Gzip(new byte[MaxPayloadBytes + 1]);
        Assert.True(compressed.Length < MaxPayloadBytes / 64, $"the bomb should be small on the wire, was {compressed.Length} bytes");
        var context = NewOtlpEndpointContext(OtlpPayloadParser.JsonContentType, compressed);
        context.Request.Headers.ContentEncoding = "gzip";

        var result = await CollectorEndpointExtensions.IngestOtlpLogsAsync(
            context, store, TestContext.Current.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.Equal(OtlpPayloadParser.JsonContentType, context.Response.ContentType);
        var status = JsonParser.Default.Parse<RpcStatus>(Encoding.UTF8.GetString(ResponseBytes(context)));
        Assert.Equal(OtlpPayloadParser.PayloadTooLargeMessage, status.Message);
    }

    [Fact]
    public async Task A_body_at_the_bound_is_decoded_not_refused()
    {
        await using var store = new DuckDbStore(":memory:");
        // Exactly the bound of zero bytes: accepted by the size check, then refused as undecodable.
        var context = NewOtlpEndpointContext(OtlpPayloadParser.ProtobufContentType, new byte[MaxPayloadBytes]);

        var result = await CollectorEndpointExtensions.IngestOtlpMetricsAsync(
            context, store, TestContext.Current.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
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
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.Request.ContentType = contentType;
        context.Request.ContentLength = payload.Length;
        context.Request.Body = new MemoryStream(payload);
        return context;
    }

    private static byte[] ResponseBytes(DefaultHttpContext context) =>
        Assert.IsType<MemoryStream>(context.Response.Body).ToArray();
}
