namespace Qyl.Collector.Ingestion;

using Grpc;

/// <summary>
///     Parses OTLP ExportLogsServiceRequest from HTTP protobuf payloads.
///     Mirrors <see cref="OtlpProtobufParser" /> for traces.
/// </summary>
public static class OtlpLogProtobufParser
{
    public static ExportLogsServiceRequestProto Parse(ReadOnlyMemory<byte> data) =>
        Parse(new ReadOnlySequence<byte>(data));

    public static ExportLogsServiceRequestProto Parse(ReadOnlySequence<byte> data)
    {
        var request = new ExportLogsServiceRequestProto();
        request.MergeFrom(data);
        return request;
    }

    public static async Task<ExportLogsServiceRequestProto> ParseFromRequestAsync(
        HttpRequest request,
        CancellationToken ct = default)
    {
        request.EnableBuffering();
        await using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms, ct).ConfigureAwait(false);
        return Parse(ms.GetBuffer().AsMemory(0, (int)ms.Length));
    }
}
