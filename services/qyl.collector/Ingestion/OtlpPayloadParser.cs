using Google.Protobuf;
using System.IO.Compression;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Qyl.Collector.Ingestion;

internal static class OtlpPayloadParser
{
    private const int MaxPayloadBytes = 16 * 1024 * 1024;
    private static readonly JsonParser OtlpJsonParser =
        new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));
    internal const string ProtobufContentType = "application/x-protobuf";
    internal const string JsonContentType = "application/json";

    public static OtlpPayloadEncoding GetEncoding(string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType) &&
            contentType.StartsWith(ProtobufContentType, StringComparison.OrdinalIgnoreCase))
            return OtlpPayloadEncoding.Protobuf;

        if (!string.IsNullOrWhiteSpace(contentType) &&
            contentType.StartsWith(JsonContentType, StringComparison.OrdinalIgnoreCase))
            return OtlpPayloadEncoding.Json;

        throw new OtlpUnsupportedMediaTypeException(contentType);
    }

    public static Task<ExportTraceServiceRequest> ParseTraceRequestAsync(
        HttpRequest request,
        OtlpPayloadEncoding encoding,
        CancellationToken ct = default) =>
        encoding is OtlpPayloadEncoding.Protobuf
            ? ParseProtobufAsync(request, ExportTraceServiceRequest.Parser, ct)
            : ParseJsonAsync<ExportTraceServiceRequest>(request, ct);

    public static Task<ExportLogsServiceRequest> ParseLogsRequestAsync(
        HttpRequest request,
        OtlpPayloadEncoding encoding,
        CancellationToken ct = default) =>
        encoding is OtlpPayloadEncoding.Protobuf
            ? ParseProtobufAsync(request, ExportLogsServiceRequest.Parser, ct)
            : ParseJsonAsync<ExportLogsServiceRequest>(request, ct);

    public static Task<ExportProfilesServiceRequest> ParseProfilesRequestAsync(
        HttpRequest request,
        OtlpPayloadEncoding encoding,
        CancellationToken ct = default) =>
        encoding is OtlpPayloadEncoding.Protobuf
            ? ParseProtobufAsync(request, ExportProfilesServiceRequest.Parser, ct)
            : ParseJsonAsync<ExportProfilesServiceRequest>(request, ct);

    private static async Task<T> ParseProtobufAsync<T>(
        HttpRequest request,
        MessageParser<T> parser,
        CancellationToken ct)
        where T : IMessage<T>
    {
        await using var payload = await ReadBoundedPayloadAsync(request, ct).ConfigureAwait(false);
        return parser.ParseFrom(payload);
    }

    private static async Task<T> ParseJsonAsync<T>(HttpRequest request, CancellationToken ct)
        where T : IMessage<T>, new()
    {
        await using var payload = await ReadBoundedPayloadAsync(request, ct).ConfigureAwait(false);

        // The OTLP spec mandates hex for trace/span/profile ids in JSON, but protojson decodes
        // bytes fields as base64 — rewrite the id fields (validating them) before parsing.
        var json = OtlpJsonIdNormalizer.NormalizeIdsToProtoJson(payload.GetBuffer().AsSpan(0, (int)payload.Length));
        return OtlpJsonParser.Parse<T>(json);
    }

    private static async Task<MemoryStream> ReadBoundedPayloadAsync(HttpRequest request, CancellationToken ct)
    {
        request.EnableBuffering();
        var contentEncoding = request.Headers["Content-Encoding"].ToString().Trim();
        var isIdentity = contentEncoding.Length is 0 ||
                         string.Equals(contentEncoding, "identity", StringComparison.OrdinalIgnoreCase);
        if (!isIdentity && !string.Equals(contentEncoding, "gzip", StringComparison.OrdinalIgnoreCase))
            throw new OtlpUnsupportedContentEncodingException(contentEncoding);

        if (isIdentity && request.ContentLength is > MaxPayloadBytes)
            throw new InvalidDataException("OTLP payload exceeds the configured maximum size.");

        try
        {
            if (isIdentity)
            {
                var capacity = request.ContentLength is > 0 and <= int.MaxValue
                    ? (int)request.ContentLength.Value
                    : 0;
                return await ReadBoundedStreamAsync(request.Body, capacity, ct).ConfigureAwait(false);
            }

            await using var gzip = new GZipStream(request.Body, CompressionMode.Decompress, leaveOpen: true);
            return await ReadBoundedStreamAsync(gzip, 0, ct).ConfigureAwait(false);
        }
        finally
        {
            if (request.Body.CanSeek)
                request.Body.Position = 0;
        }
    }

    private static async Task<MemoryStream> ReadBoundedStreamAsync(
        Stream source,
        int capacity,
        CancellationToken ct)
    {
        var payload = new MemoryStream(capacity);
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        long totalBytes = 0;

        try
        {
            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
                if (read is 0)
                    break;

                totalBytes += read;
                if (totalBytes > MaxPayloadBytes)
                    throw new InvalidDataException("OTLP payload exceeds the configured maximum size.");

                payload.Write(buffer, 0, read);
            }

            payload.Position = 0;
            return payload;
        }
        catch
        {
            await payload.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
