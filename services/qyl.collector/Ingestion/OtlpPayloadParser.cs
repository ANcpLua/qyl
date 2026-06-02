using System.Text;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Qyl.Collector.Ingestion;

internal static class OtlpPayloadParser
{
    private const int MaxPayloadBytes = 16 * 1024 * 1024;
    private const string ProtobufContentType = "application/x-protobuf";

    public static bool IsProtobufContentType(string? contentType) =>
        !string.IsNullOrEmpty(contentType) && contentType.StartsWithIgnoreCase(ProtobufContentType);

    public static Task<ExportTraceServiceRequest> ParseTraceRequestAsync(
        HttpRequest request,
        CancellationToken ct = default) =>
        IsProtobufContentType(request.ContentType)
            ? ParseProtobufAsync(request, ExportTraceServiceRequest.Parser, ct)
            : ParseJsonAsync<ExportTraceServiceRequest>(request, ct);

    public static Task<ExportLogsServiceRequest> ParseLogsRequestAsync(
        HttpRequest request,
        CancellationToken ct = default) =>
        IsProtobufContentType(request.ContentType)
            ? ParseProtobufAsync(request, ExportLogsServiceRequest.Parser, ct)
            : ParseJsonAsync<ExportLogsServiceRequest>(request, ct);

    public static Task<ExportProfilesServiceRequest> ParseProfilesRequestAsync(
        HttpRequest request,
        CancellationToken ct = default) =>
        IsProtobufContentType(request.ContentType)
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
        using var reader = new StreamReader(payload, Encoding.UTF8);
        var json = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        return JsonParser.Default.Parse<T>(json);
    }

    private static async Task<MemoryStream> ReadBoundedPayloadAsync(HttpRequest request, CancellationToken ct)
    {
        if (request.ContentLength is > MaxPayloadBytes)
            throw new InvalidDataException("OTLP payload exceeds the configured maximum size.");

        request.EnableBuffering();
        var capacity = request.ContentLength is > 0 and <= int.MaxValue ? (int)request.ContentLength.Value : 0;
        var payload = new MemoryStream(capacity);
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        long totalBytes = 0;

        try
        {
            while (true)
            {
                var read = await request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
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
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            request.Body.Position = 0;
        }
    }
}
