namespace Qyl.Collector.Grpc;

/// <summary>
///     Request message for LogsService.Export (protobuf wire format).
///     Proto: opentelemetry.proto.collector.logs.v1.ExportLogsServiceRequest
/// </summary>
public sealed class ExportLogsServiceRequestProto
{
    public List<OtlpResourceLogsProto> ResourceLogs { get; } = [];

    public void MergeFrom(ReadOnlySequence<byte> data)
    {
        var reader = new ProtobufReader(data);
        while (reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 1:
                    var resourceLogs = new OtlpResourceLogsProto();
                    reader.ReadMessage(resourceLogs);
                    ResourceLogs.Add(resourceLogs);
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}

public sealed class OtlpResourceLogsProto : IProtobufParseable
{
    public OtlpResourceProto? Resource { get; set; }
    public List<OtlpScopeLogsProto> ScopeLogs { get; } = [];

    public void MergeFrom(ProtobufReader reader, int length)
    {
        var endPosition = reader.Position + length;
        while (reader.Position < endPosition && reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 1:
                    Resource = new OtlpResourceProto();
                    reader.ReadMessage(Resource);
                    break;
                case 2:
                    var scopeLogs = new OtlpScopeLogsProto();
                    reader.ReadMessage(scopeLogs);
                    ScopeLogs.Add(scopeLogs);
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}

public sealed class OtlpScopeLogsProto : IProtobufParseable
{
    public List<OtlpLogRecordProto> LogRecords { get; } = [];

    public void MergeFrom(ProtobufReader reader, int length)
    {
        var endPosition = reader.Position + length;
        while (reader.Position < endPosition && reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 2:
                    var logRecord = new OtlpLogRecordProto();
                    reader.ReadMessage(logRecord);
                    LogRecords.Add(logRecord);
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}

/// <summary>
///     LogRecord from OTLP proto.
///     Field numbers: opentelemetry.proto.logs.v1.LogRecord
/// </summary>
public sealed class OtlpLogRecordProto : IProtobufParseable
{
    public ulong TimeUnixNano { get; set; }
    public ulong ObservedTimeUnixNano { get; set; }
    public int SeverityNumber { get; set; }
    public string? SeverityText { get; set; }
    public OtlpAnyValueProto? Body { get; set; }
    public List<OtlpKeyValueProto>? Attributes { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }

    public void MergeFrom(ProtobufReader reader, int length)
    {
        var endPosition = reader.Position + length;
        while (reader.Position < endPosition && reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 1: // time_unix_nano (fixed64)
                    TimeUnixNano = reader.ReadFixed64();
                    break;
                case 2: // severity_number (enum)
                    SeverityNumber = (int)reader.ReadVarint();
                    break;
                case 3: // severity_text
                    SeverityText = reader.ReadString();
                    break;
                case 5: // body (AnyValue)
                    Body = new OtlpAnyValueProto();
                    reader.ReadMessage(Body);
                    break;
                case 6: // attributes
                    Attributes ??= [];
                    var attr = new OtlpKeyValueProto();
                    reader.ReadMessage(attr);
                    Attributes.Add(attr);
                    break;
                case 9: // trace_id (bytes)
                    TraceId = reader.ReadBytesAsHex();
                    break;
                case 10: // span_id (bytes)
                    SpanId = reader.ReadBytesAsHex();
                    break;
                case 11: // observed_time_unix_nano (fixed64)
                    ObservedTimeUnixNano = reader.ReadFixed64();
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}
