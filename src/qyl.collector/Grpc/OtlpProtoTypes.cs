namespace qyl.collector.Grpc;

#region gRPC Service Base and Method Provider

/// <summary>
///     Base class for OTLP TraceService gRPC implementation.
///     Service: opentelemetry.proto.collector.trace.v1.TraceService
/// </summary>
public abstract class TraceServiceBase
{
    /// <summary>
    ///     Exports trace data (spans) to the collector.
    /// </summary>
    public abstract Task<ExportTraceServiceResponse> Export(
        ExportTraceServiceRequest request,
        ServerCallContext context);
}

/// <summary>
///     Service method provider for TraceServiceImpl.
///     This tells ASP.NET Core gRPC how to invoke methods on our custom service.
/// </summary>
public sealed class TraceServiceMethodProvider : IServiceMethodProvider<TraceServiceImpl>
{
    /// <summary>
    ///     Discovers and registers gRPC methods for TraceServiceImpl.
    /// </summary>
    public void OnServiceMethodDiscovery(ServiceMethodProviderContext<TraceServiceImpl> context)
    {
        var exportMethod = new Method<ExportTraceServiceRequest, ExportTraceServiceResponse>(
            MethodType.Unary,
            "opentelemetry.proto.collector.trace.v1.TraceService",
            "Export",
            ExportTraceServiceRequest.Marshaller,
            ExportTraceServiceResponse.Marshaller);

        context.AddUnaryMethod(
            exportMethod,
            [],
            async static (service, request, serverCallContext) =>
                await service.Export(request, serverCallContext).ConfigureAwait(false));
    }
}

#endregion

#region Request/Response Messages

/// <summary>
///     Request message for TraceService.Export.
/// </summary>
public sealed class ExportTraceServiceRequest
{
    public List<OtlpResourceSpansProto> ResourceSpans { get; } = [];

    public static Marshaller<ExportTraceServiceRequest> Marshaller { get; } = Marshallers.Create(
        (_, _) => throw new NotSupportedException("Serialization not supported for server-side request"),
        context =>
        {
            var request = new ExportTraceServiceRequest();
            request.MergeFrom(context.PayloadAsReadOnlySequence());
            return request;
        });

    public void MergeFrom(ReadOnlySequence<byte> data)
    {
        var reader = new ProtobufReader(data);
        while (reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 1: // resource_spans
                    var resourceSpan = new OtlpResourceSpansProto();
                    reader.ReadMessage(resourceSpan);
                    ResourceSpans.Add(resourceSpan);
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}

/// <summary>
///     Response message for TraceService.Export.
/// </summary>
public sealed class ExportTraceServiceResponse
{
    public static Marshaller<ExportTraceServiceResponse> Marshaller { get; } = Marshallers.Create(
static (_, _) =>
        {
            /* Empty response for success (per OTLP spec) */
        },
static _ => new ExportTraceServiceResponse());
}

#endregion

#region OTLP Proto Types

/// <summary>
///     ResourceSpans from OTLP proto.
/// </summary>
public sealed class OtlpResourceSpansProto : IProtobufParseable
{
    public OtlpResourceProto? Resource { get; set; }
    public List<OtlpScopeSpansProto> ScopeSpans { get; } = [];

    public void MergeFrom(ProtobufReader reader, int length)
    {
        var endPosition = reader.Position + length;
        while (reader.Position < endPosition && reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 1: // resource
                    Resource = new OtlpResourceProto();
                    reader.ReadMessage(Resource);
                    break;
                case 2: // scope_spans
                    var scopeSpan = new OtlpScopeSpansProto();
                    reader.ReadMessage(scopeSpan);
                    ScopeSpans.Add(scopeSpan);
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}

/// <summary>
///     Resource from OTLP proto.
/// </summary>
public sealed class OtlpResourceProto : IProtobufParseable
{
    public List<OtlpKeyValueProto> Attributes { get; } = [];

    public void MergeFrom(ProtobufReader reader, int length)
    {
        var endPosition = reader.Position + length;
        while (reader.Position < endPosition && reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 1: // attributes
                    var attr = new OtlpKeyValueProto();
                    reader.ReadMessage(attr);
                    Attributes.Add(attr);
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}

/// <summary>
///     ScopeSpans from OTLP proto.
/// </summary>
public sealed class OtlpScopeSpansProto : IProtobufParseable
{
    public List<OtlpSpanProto> Spans { get; } = [];

    public void MergeFrom(ProtobufReader reader, int length)
    {
        var endPosition = reader.Position + length;
        while (reader.Position < endPosition && reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 2: // spans
                    var span = new OtlpSpanProto();
                    reader.ReadMessage(span);
                    Spans.Add(span);
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}

/// <summary>
///     Span from OTLP proto.
/// </summary>
public sealed class OtlpSpanProto : IProtobufParseable
{
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? ParentSpanId { get; set; }
    public string? Name { get; set; }
    public int? Kind { get; set; }
    public ulong StartTimeUnixNano { get; set; }
    public ulong EndTimeUnixNano { get; set; }
    public List<OtlpKeyValueProto>? Attributes { get; set; }
    public List<OtlpSpanEventProto>? Events { get; set; }
    public OtlpStatusProto? Status { get; set; }

    public void MergeFrom(ProtobufReader reader, int length)
    {
        var endPosition = reader.Position + length;
        while (reader.Position < endPosition && reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 1: // trace_id (bytes)
                    TraceId = reader.ReadBytesAsHex();
                    break;
                case 2: // span_id (bytes)
                    SpanId = reader.ReadBytesAsHex();
                    break;
                case 4: // parent_span_id (bytes)
                    ParentSpanId = reader.ReadBytesAsHex();
                    break;
                case 5: // name
                    Name = reader.ReadString();
                    break;
                case 6: // kind (enum)
                    Kind = (int)reader.ReadVarint();
                    break;
                case 7: // start_time_unix_nano (fixed64)
                    StartTimeUnixNano = reader.ReadFixed64();
                    break;
                case 8: // end_time_unix_nano (fixed64)
                    EndTimeUnixNano = reader.ReadFixed64();
                    break;
                case 9: // attributes
                    Attributes ??= [];
                    var attr = new OtlpKeyValueProto();
                    reader.ReadMessage(attr);
                    Attributes.Add(attr);
                    break;
                case 11: // events
                    Events ??= [];
                    var evt = new OtlpSpanEventProto();
                    reader.ReadMessage(evt);
                    Events.Add(evt);
                    break;
                case 15: // status
                    Status = new OtlpStatusProto();
                    reader.ReadMessage(Status);
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}

/// <summary>
///     SpanEvent from OTLP proto.
/// </summary>
public sealed class OtlpSpanEventProto : IProtobufParseable
{
    public ulong TimeUnixNano { get; set; }
    public string? Name { get; set; }
    public List<OtlpKeyValueProto>? Attributes { get; set; }

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
                case 2: // name
                    Name = reader.ReadString();
                    break;
                case 3: // attributes
                    Attributes ??= [];
                    var attr = new OtlpKeyValueProto();
                    reader.ReadMessage(attr);
                    Attributes.Add(attr);
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}

/// <summary>
///     Status from OTLP proto.
/// </summary>
public sealed class OtlpStatusProto : IProtobufParseable
{
    public string? Message { get; set; }
    public int? Code { get; set; }

    public void MergeFrom(ProtobufReader reader, int length)
    {
        var endPosition = reader.Position + length;
        while (reader.Position < endPosition && reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 2: // message
                    Message = reader.ReadString();
                    break;
                case 3: // code (enum)
                    Code = (int)reader.ReadVarint();
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}

/// <summary>
///     KeyValue from OTLP proto.
/// </summary>
public sealed class OtlpKeyValueProto : IProtobufParseable
{
    public string? Key { get; set; }
    public OtlpAnyValueProto? Value { get; set; }

    public void MergeFrom(ProtobufReader reader, int length)
    {
        var endPosition = reader.Position + length;
        while (reader.Position < endPosition && reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 1: // key
                    Key = reader.ReadString();
                    break;
                case 2: // value
                    Value = new OtlpAnyValueProto();
                    reader.ReadMessage(Value);
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}

/// <summary>
///     AnyValue from OTLP proto (oneof value type).
/// </summary>
public sealed class OtlpAnyValueProto : IProtobufParseable
{
    public string? StringValue { get; set; }
    public bool? BoolValue { get; set; }
    public long? IntValue { get; set; }
    public double? DoubleValue { get; set; }
    public List<OtlpAnyValueProto>? ArrayValue { get; set; }
    public List<OtlpKeyValueProto>? KvlistValue { get; set; }
    public byte[]? BytesValue { get; set; }

    public void MergeFrom(ProtobufReader reader, int length)
    {
        var endPosition = reader.Position + length;
        while (reader.Position < endPosition && reader.TryReadTag(out var tag))
        {
            switch (tag.FieldNumber)
            {
                case 1: // string_value
                    StringValue = reader.ReadString();
                    break;
                case 2: // bool_value
                    BoolValue = reader.ReadVarint() is not 0;
                    break;
                case 3: // int_value
                    IntValue = reader.ReadSignedVarint();
                    break;
                case 4: // double_value
                    DoubleValue = reader.ReadDouble();
                    break;
                case 5: // array_value
                    ArrayValue ??= [];
                    var arrayLen = (int)reader.ReadVarint();
                    var arrayEnd = reader.Position + arrayLen;
                    while (reader.Position < arrayEnd && reader.TryReadTag(out var arrTag))
                    {
                        if (arrTag.FieldNumber == 1)
                        {
                            var item = new OtlpAnyValueProto();
                            reader.ReadMessage(item);
                            ArrayValue.Add(item);
                        }
                        else
                            reader.SkipField(arrTag.WireType);
                    }

                    break;
                case 6: // kvlist_value
                    KvlistValue ??= [];
                    var kvLen = (int)reader.ReadVarint();
                    var kvEnd = reader.Position + kvLen;
                    while (reader.Position < kvEnd && reader.TryReadTag(out var kvTag))
                    {
                        if (kvTag.FieldNumber == 1)
                        {
                            var kv = new OtlpKeyValueProto();
                            reader.ReadMessage(kv);
                            KvlistValue.Add(kv);
                        }
                        else
                            reader.SkipField(kvTag.WireType);
                    }

                    break;
                case 7: // bytes_value
                    BytesValue = reader.ReadBytes();
                    break;
                default:
                    reader.SkipField(tag.WireType);
                    break;
            }
        }
    }
}

#endregion

#region Protobuf Reader Infrastructure

/// <summary>
///     Interface for types that can be parsed from protobuf.
/// </summary>
public interface IProtobufParseable
{
    void MergeFrom(ProtobufReader reader, int length);
}

/// <summary>
///     Tag information from protobuf wire format.
/// </summary>
public readonly record struct ProtobufTag(int FieldNumber, WireType WireType);

/// <summary>
///     Wire types in protobuf encoding.
/// </summary>
public enum WireType
{
    Varint = 0,
    Fixed64 = 1,
    LengthDelimited = 2,
    StartGroup = 3,
    EndGroup = 4,
    Fixed32 = 5
}

/// <summary>
///     Simple protobuf reader for parsing OTLP messages.
///     Zero-allocation for contiguous buffers.
/// </summary>
public ref struct ProtobufReader(ReadOnlySequence<byte> sequence)
{
    private readonly ReadOnlySpan<byte> _buffer = sequence.IsSingleSegment
        ? sequence.FirstSpan
        : sequence.ToArray();

    public int Position { get; private set; } = 0;

    public bool TryReadTag(out ProtobufTag tag)
    {
        if (Position >= _buffer.Length)
        {
            tag = default;
            return false;
        }

        var value = ReadVarint();
        var fieldNumber = (int)(value >> 3);
        var wireType = (WireType)(value & 0x7);
        tag = new ProtobufTag(fieldNumber, wireType);
        return true;
    }

    public ulong ReadVarint()
    {
        ulong result = 0;
        var shift = 0;
        byte b;
        do
        {
            if (Position >= _buffer.Length)
                throw new InvalidOperationException("Unexpected end of buffer reading varint");
            b = _buffer[Position++];
            result |= (ulong)(b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) is not 0);

        return result;
    }

    public long ReadSignedVarint()
    {
        var value = ReadVarint();
        return (long)(value >> 1) ^ -((long)value & 1);
    }

    public ulong ReadFixed64()
    {
        if (Position + 8 > _buffer.Length)
            throw new InvalidOperationException("Unexpected end of buffer reading fixed64");
        var result = BitConverter.ToUInt64(_buffer.Slice(Position, 8));
        Position += 8;
        return result;
    }

    public double ReadDouble()
    {
        if (Position + 8 > _buffer.Length)
            throw new InvalidOperationException("Unexpected end of buffer reading double");
        var result = BitConverter.ToDouble(_buffer.Slice(Position, 8));
        Position += 8;
        return result;
    }

    public string ReadString()
    {
        var length = (int)ReadVarint();
        if (Position + length > _buffer.Length)
            throw new InvalidOperationException("Unexpected end of buffer reading string");
        var result = Encoding.UTF8.GetString(_buffer.Slice(Position, length));
        Position += length;
        return result;
    }

    public byte[] ReadBytes()
    {
        var length = (int)ReadVarint();
        if (Position + length > _buffer.Length)
            throw new InvalidOperationException("Unexpected end of buffer reading bytes");
        var result = _buffer.Slice(Position, length).ToArray();
        Position += length;
        return result;
    }

    public string ReadBytesAsHex()
    {
        var bytes = ReadBytes();
        return bytes.Length > 0 ? Convert.ToHexStringLower(bytes) : "";
    }

    public void ReadMessage(IProtobufParseable message)
    {
        var length = (int)ReadVarint();
        message.MergeFrom(this, length);
    }

    public void SkipField(WireType wireType)
    {
        switch (wireType)
        {
            case WireType.Varint:
                ReadVarint();
                break;
            case WireType.Fixed64:
                Position += 8;
                break;
            case WireType.LengthDelimited:
                var length = (int)ReadVarint();
                Position += length;
                break;
            case WireType.Fixed32:
                Position += 4;
                break;
            case WireType.StartGroup:
            case WireType.EndGroup:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(wireType), wireType, null);
        }
    }
}

#endregion
