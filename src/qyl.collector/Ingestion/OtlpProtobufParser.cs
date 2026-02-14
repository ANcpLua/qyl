// =============================================================================
// qyl OTLP Ingestion - HTTP Protobuf Parser
// Target: .NET 10 / C# 14 | OTel Semantic Conventions 1.39.0
// =============================================================================

using qyl.collector.Grpc;

namespace qyl.collector.Ingestion;

/// <summary>
///     Parses OTLP ExportTraceServiceRequest from HTTP protobuf payloads.
///     Used by: POST /v1/traces with Content-Type: application/x-protobuf
///     Reuses the same proto types as the gRPC endpoint for consistency.
/// </summary>
public static class OtlpProtobufParser
{
    /// <summary>
    ///     Content type for OTLP HTTP protobuf format.
    /// </summary>
    public const string ContentType = "application/x-protobuf";

    /// <summary>
    ///     Parses an ExportTraceServiceRequest from a protobuf-encoded byte array.
    /// </summary>
    /// <param name="data">The protobuf-encoded request body.</param>
    /// <returns>The parsed ExportTraceServiceRequest.</returns>
    public static ExportTraceServiceRequest Parse(ReadOnlyMemory<byte> data)
    {
        var request = new ExportTraceServiceRequest();
        request.MergeFrom(new ReadOnlySequence<byte>(data));
        return request;
    }

    /// <summary>
    ///     Parses an ExportTraceServiceRequest from a protobuf-encoded ReadOnlySequence.
    /// </summary>
    /// <param name="data">The protobuf-encoded request body.</param>
    /// <returns>The parsed ExportTraceServiceRequest.</returns>
    public static ExportTraceServiceRequest Parse(ReadOnlySequence<byte> data)
    {
        var request = new ExportTraceServiceRequest();
        request.MergeFrom(data);
        return request;
    }

    /// <summary>
    ///     Reads the request body and parses as OTLP protobuf.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed ExportTraceServiceRequest.</returns>
    public static async Task<ExportTraceServiceRequest> ParseFromRequestAsync(
        HttpRequest request,
        CancellationToken ct = default)
    {
        // Enable request body buffering for potential re-reads
        request.EnableBuffering();

        // Read the entire body into memory
        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms, ct).ConfigureAwait(false);

        var bytes = ms.ToArray();
        return Parse(bytes.AsMemory());
    }

    /// <summary>
    ///     Checks if the request content type indicates protobuf format.
    /// </summary>
    /// <param name="contentType">The Content-Type header value.</param>
    /// <returns>True if the content type indicates protobuf.</returns>
    public static bool IsProtobufContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;

        // Handle content types like "application/x-protobuf" or "application/x-protobuf; charset=utf-8"
        return contentType.StartsWithIgnoreCase(ContentType);
    }
}
