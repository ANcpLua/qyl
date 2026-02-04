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

    [Fact]
    public void Parse_WithGenAiAttributes_ExtractsCorrectly()
    {
        // Arrange - Create proto with GenAI attributes
        var protoBytes = CreateProtoWithGenAiAttributes(
            providerName: "openai",
            requestModel: "gpt-4");

        // Act
        var request = OtlpProtobufParser.Parse(protoBytes.AsMemory());
        var spans = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        Assert.Single(spans);
        Assert.Equal("openai", spans[0].GenAiProviderName);
        Assert.Equal("gpt-4", spans[0].GenAiRequestModel);
    }

    // =========================================================================
    // Round-Trip Tests (Proto -> Parse -> Convert -> Storage)
    // =========================================================================

    [Fact]
    public void RoundTrip_ProtobufToStorageRow_PreservesData()
    {
        // Arrange
        var protoBytes = CreateDetailedProtoPayload(
            traceId: "4bf92f3577b34da6a3ce929d0e0e4736",
            spanId: "00f067aa0ba902b7",
            name: "test-operation",
            serviceName: "test-service");

        // Act - Parse from bytes
        var request = OtlpProtobufParser.Parse(protoBytes.AsMemory());

        // Convert to storage rows
        var spans = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        Assert.Single(spans);
        var span = spans[0];
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", span.TraceId);
        Assert.Equal("00f067aa0ba902b7", span.SpanId);
        Assert.Equal("test-operation", span.Name);
        Assert.Equal("test-service", span.ServiceName);
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
        WriteResourceSpans(resourceSpansMs, "test-service", "trace123", "span123", "test-span");
        WriteBytes(ms, resourceSpansMs.ToArray());

        return ms.ToArray();
    }

    /// <summary>
    ///     Creates a proto payload with GenAI attributes.
    /// </summary>
    private static byte[] CreateProtoWithGenAiAttributes(string providerName, string requestModel)
    {
        using var ms = new MemoryStream();

        // ExportTraceServiceRequest.resource_spans (field 1, length-delimited)
        WriteTag(ms, 1, WireType.LengthDelimited);
        using var resourceSpansMs = new MemoryStream();
        WriteResourceSpansWithGenAi(resourceSpansMs, "genai-service", "trace123", "span123", "chat gpt-4",
            providerName, requestModel);
        WriteBytes(ms, resourceSpansMs.ToArray());

        return ms.ToArray();
    }

    /// <summary>
    ///     Creates a detailed proto payload with specific values for round-trip testing.
    /// </summary>
    private static byte[] CreateDetailedProtoPayload(
        string traceId,
        string spanId,
        string name,
        string serviceName)
    {
        using var ms = new MemoryStream();

        // ExportTraceServiceRequest.resource_spans (field 1, length-delimited)
        WriteTag(ms, 1, WireType.LengthDelimited);
        using var resourceSpansMs = new MemoryStream();
        WriteResourceSpans(resourceSpansMs, serviceName, traceId, spanId, name);
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
        WriteScopeSpans(scopeSpansMs, traceId, spanId, name, null, null);
        WriteBytes(ms, scopeSpansMs.ToArray());
    }

    private static void WriteResourceSpansWithGenAi(MemoryStream ms, string serviceName, string traceId, string spanId,
        string name, string providerName, string requestModel)
    {
        // ResourceSpans.resource (field 1, length-delimited)
        WriteTag(ms, 1, WireType.LengthDelimited);
        using var resourceMs = new MemoryStream();
        WriteResource(resourceMs, serviceName);
        WriteBytes(ms, resourceMs.ToArray());

        // ResourceSpans.scope_spans (field 2, length-delimited)
        WriteTag(ms, 2, WireType.LengthDelimited);
        using var scopeSpansMs = new MemoryStream();
        WriteScopeSpans(scopeSpansMs, traceId, spanId, name, providerName, requestModel);
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

    private static void WriteScopeSpans(MemoryStream ms, string traceId, string spanId, string name,
        string? providerName, string? requestModel)
    {
        // ScopeSpans.spans (field 2, length-delimited)
        WriteTag(ms, 2, WireType.LengthDelimited);
        using var spanMs = new MemoryStream();
        WriteSpan(spanMs, traceId, spanId, name, providerName, requestModel);
        WriteBytes(ms, spanMs.ToArray());
    }

    private static void WriteSpan(MemoryStream ms, string traceId, string spanId, string name, string? providerName,
        string? requestModel)
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

        // Span.attributes (field 9, length-delimited) - GenAI attributes
        if (providerName is not null)
        {
            WriteTag(ms, 9, WireType.LengthDelimited);
            using var attrMs = new MemoryStream();
            WriteKeyValue(attrMs, "gen_ai.provider.name", providerName);
            WriteBytes(ms, attrMs.ToArray());
        }

        if (requestModel is not null)
        {
            WriteTag(ms, 9, WireType.LengthDelimited);
            using var attrMs = new MemoryStream();
            WriteKeyValue(attrMs, "gen_ai.request.model", requestModel);
            WriteBytes(ms, attrMs.ToArray());
        }
    }

    private static void WriteKeyValue(MemoryStream ms, string key, string value)
    {
        // KeyValue.key (field 1, string)
        WriteTag(ms, 1, WireType.LengthDelimited);
        WriteString(ms, key);

        // KeyValue.value (field 2, AnyValue)
        WriteTag(ms, 2, WireType.LengthDelimited);
        using var anyValueMs = new MemoryStream();
        WriteAnyValue(anyValueMs, value);
        WriteBytes(ms, anyValueMs.ToArray());
    }

    private static void WriteAnyValue(MemoryStream ms, string value)
    {
        // AnyValue.string_value (field 1, string)
        WriteTag(ms, 1, WireType.LengthDelimited);
        WriteString(ms, value);
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
