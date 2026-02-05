using System.Buffers;
using qyl.collector.Grpc;
using qyl.collector.Ingestion;

namespace qyl.collector.tests.Ingestion;

/// <summary>
///     Tests for OtlpProtobufParser - HTTP protobuf ingestion path.
/// </summary>
public sealed class OtlpProtobufParserTests
{
    // =========================================================================
    // Content Type Detection Tests
    // =========================================================================

    [Theory]
    [InlineData("application/x-protobuf", true)]
    [InlineData("application/x-protobuf; charset=utf-8", true)]
    [InlineData("APPLICATION/X-PROTOBUF", true)]
    [InlineData("application/json", false)]
    [InlineData("text/plain", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsProtobufContentType_ReturnsExpected(string? contentType, bool expected)
    {
        // Act
        var result = OtlpProtobufParser.IsProtobufContentType(contentType);

        // Assert
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // Parsing Tests
    // =========================================================================

    [Fact]
    public void Parse_EmptyData_ReturnsEmptyRequest()
    {
        // Arrange
        var data = ReadOnlyMemory<byte>.Empty;

        // Act
        var request = OtlpProtobufParser.Parse(data);

        // Assert
        Assert.NotNull(request);
        Assert.Empty(request.ResourceSpans);
    }

    [Fact]
    public void Parse_ValidProtobuf_ReturnsPopulatedRequest()
    {
        // Arrange - Create a valid OTLP protobuf payload
        var protoBytes = CreateMinimalProtoPayload();

        // Act
        var request = OtlpProtobufParser.Parse(protoBytes.AsMemory());

        // Assert
        Assert.NotNull(request);
        Assert.NotEmpty(request.ResourceSpans);
    }

    [Fact]
    public void Parse_ReadOnlySequence_ParsesCorrectly()
    {
        // Arrange
        var protoBytes = CreateMinimalProtoPayload();
        var sequence = new ReadOnlySequence<byte>(protoBytes);

        // Act
        var request = OtlpProtobufParser.Parse(sequence);

        // Assert
        Assert.NotNull(request);
        Assert.NotEmpty(request.ResourceSpans);
    }


    // =========================================================================
    // Helper Methods - Protobuf Encoding
    // =========================================================================

    /// <summary>
    ///     Creates a minimal valid OTLP protobuf payload.
    /// </summary>
    private static byte[] CreateMinimalProtoPayload()
    {
        using var ms = new MemoryStream();

        // ExportTraceServiceRequest.resource_spans (field 1, length-delimited)
        WriteTag(ms, 1, WireType.LengthDelimited);
        using var resourceSpansMs = new MemoryStream();
        WriteResourceSpans(resourceSpansMs, "test-service", "00000000000000000000000000000001", "0000000000000001", "test-span");
        WriteBytes(ms, resourceSpansMs.ToArray());

        return ms.ToArray();
    }

    private static void WriteResourceSpans(MemoryStream ms, string serviceName, string traceId, string spanId,
        string name)
    {
        // ResourceSpans.resource (field 1, length-delimited)
        WriteTag(ms, 1, WireType.LengthDelimited);
        using var resourceMs = new MemoryStream();
        WriteResource(resourceMs, serviceName);
        WriteBytes(ms, resourceMs.ToArray());

        // ResourceSpans.scope_spans (field 2, length-delimited)
        WriteTag(ms, 2, WireType.LengthDelimited);
        using var scopeSpansMs = new MemoryStream();
        WriteScopeSpans(scopeSpansMs, traceId, spanId, name);
        WriteBytes(ms, scopeSpansMs.ToArray());
    }

    private static void WriteResource(MemoryStream ms, string serviceName)
    {
        // Resource.attributes (field 1, length-delimited)
        WriteTag(ms, 1, WireType.LengthDelimited);
        using var attrMs = new MemoryStream();
        WriteKeyValue(attrMs, "service.name", serviceName);
        WriteBytes(ms, attrMs.ToArray());
    }

    private static void WriteScopeSpans(MemoryStream ms, string traceId, string spanId, string name)
    {
        // ScopeSpans.spans (field 2, length-delimited)
        WriteTag(ms, 2, WireType.LengthDelimited);
        using var spanMs = new MemoryStream();
        WriteSpan(spanMs, traceId, spanId, name);
        WriteBytes(ms, spanMs.ToArray());
    }

    private static void WriteSpan(MemoryStream ms, string traceId, string spanId, string name)
    {
        // Span.trace_id (field 1, bytes)
        WriteTag(ms, 1, WireType.LengthDelimited);
        var traceIdBytes = Convert.FromHexString(traceId);
        WriteBytes(ms, traceIdBytes);

        // Span.span_id (field 2, bytes)
        WriteTag(ms, 2, WireType.LengthDelimited);
        var spanIdBytes = Convert.FromHexString(spanId);
        WriteBytes(ms, spanIdBytes);

        // Span.name (field 5, string)
        WriteTag(ms, 5, WireType.LengthDelimited);
        WriteString(ms, name);

        // Span.kind (field 6, varint) - CLIENT = 3
        WriteTag(ms, 6, WireType.Varint);
        WriteVarint(ms, 3);

        // Span.start_time_unix_nano (field 7, fixed64)
        WriteTag(ms, 7, WireType.Fixed64);
        WriteFixed64(ms, 1000000000UL);

        // Span.end_time_unix_nano (field 8, fixed64)
        WriteTag(ms, 8, WireType.Fixed64);
        WriteFixed64(ms, 2000000000UL);
    }

    private static void WriteKeyValue(MemoryStream ms, string key, string value)
    {
        // KeyValue.key (field 1, string)
        WriteTag(ms, 1, WireType.LengthDelimited);
        WriteString(ms, key);

        // KeyValue.value (field 2, AnyValue)
        WriteTag(ms, 2, WireType.LengthDelimited);
        using var anyValueMs = new MemoryStream();
        // AnyValue.string_value (field 1, string)
        WriteTag(anyValueMs, 1, WireType.LengthDelimited);
        WriteString(anyValueMs, value);
        WriteBytes(ms, anyValueMs.ToArray());
    }

    private static void WriteTag(MemoryStream ms, int fieldNumber, WireType wireType)
    {
        WriteVarint(ms, (ulong)((fieldNumber << 3) | (int)wireType));
    }

    private static void WriteVarint(MemoryStream ms, ulong value)
    {
        while (value >= 0x80)
        {
            ms.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }

        ms.WriteByte((byte)value);
    }

    private static void WriteFixed64(MemoryStream ms, ulong value)
    {
        var bytes = BitConverter.GetBytes(value);
        ms.Write(bytes, 0, 8);
    }

    private static void WriteString(MemoryStream ms, string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        WriteVarint(ms, (ulong)bytes.Length);
        ms.Write(bytes, 0, bytes.Length);
    }

    private static void WriteBytes(MemoryStream ms, byte[] bytes)
    {
        WriteVarint(ms, (ulong)bytes.Length);
        ms.Write(bytes, 0, bytes.Length);
    }
}