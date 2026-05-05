
using Qyl.Collector.Grpc;

namespace Qyl.Collector.Ingestion;

public static class OtlpProtobufParser
{
    public const string ContentType = "application/x-protobuf";

    public static ExportTraceServiceRequest Parse(ReadOnlyMemory<byte> data) =>
        Parse(new ReadOnlySequence<byte>(data));

    public static ExportTraceServiceRequest Parse(ReadOnlySequence<byte> data)
    {
        var request = new ExportTraceServiceRequest();
        request.MergeFrom(data);
        return request;
    }

    public static async Task<ExportTraceServiceRequest> ParseFromRequestAsync(
        HttpRequest request,
        CancellationToken ct = default)
    {
        request.EnableBuffering();

        await using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms, ct).ConfigureAwait(false);

        return Parse(ms.GetBuffer().AsMemory(0, (int)ms.Length));
    }

    public static bool IsProtobufContentType(string? contentType) =>
        !string.IsNullOrEmpty(contentType) && contentType.StartsWithIgnoreCase(ContentType);
}
