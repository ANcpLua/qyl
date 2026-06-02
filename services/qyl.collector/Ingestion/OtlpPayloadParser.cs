using System.Text;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Qyl.Collector.Ingestion;

public static class OtlpPayloadParser
{
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

    private static async Task<T> ParseProtobufAsync<T>(
        HttpRequest request,
        MessageParser<T> parser,
        CancellationToken ct)
        where T : IMessage<T>
    {
        request.EnableBuffering();
        await using var payload = new MemoryStream();
        await request.Body.CopyToAsync(payload, ct).ConfigureAwait(false);
        payload.Position = 0;
        return parser.ParseFrom(payload);
    }

    private static async Task<T> ParseJsonAsync<T>(HttpRequest request, CancellationToken ct)
        where T : IMessage<T>, new()
    {
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var json = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        request.Body.Position = 0;
        return JsonParser.Default.Parse<T>(json);
    }
}
