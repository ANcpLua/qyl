using System.Buffers;
using System.Text;
using qyl.collector.Grpc;
using qyl.collector.Ingestion;

namespace qyl.collector.tests.Ingestion;

/// <summary>
///     Tests for OtlpProtobufParser - HTTP protobuf ingestion path.
///     Uses composable protobuf builders (standard wire format) for test payloads.
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
        // Arrange
        var protoBytes = BuildExportTraceServiceRequest(
            BuildResourceSpans(
                BuildResource("test-service"),
                BuildScopeSpans(
                    BuildSpan("00000000000000000000000000000001", "0000000000000001", "test-span"))));

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
        var protoBytes = BuildExportTraceServiceRequest(
            BuildResourceSpans(
                BuildResource("test-service"),
                BuildScopeSpans(
                    BuildSpan("00000000000000000000000000000001", "0000000000000001", "test-span"))));
        var sequence = new ReadOnlySequence<byte>(protoBytes);

        // Act
        var request = OtlpProtobufParser.Parse(sequence);

        // Assert
        Assert.NotNull(request);
        Assert.NotEmpty(request.ResourceSpans);
    }

    [Fact]
    public void Parse_SpanWithOneAttribute_ParsesCorrectly()
    {
        // Minimal test: one span attribute to isolate the parsing issue
        using var spanMs = new MemoryStream();
        WriteTag(spanMs, 1, WireType.LengthDelimited);
        WriteBytes(spanMs, Convert.FromHexString("00000000000000000000000000000001"));
        WriteTag(spanMs, 2, WireType.LengthDelimited);
        WriteBytes(spanMs, Convert.FromHexString("0000000000000001"));
        WriteTag(spanMs, 5, WireType.LengthDelimited);
        WriteString(spanMs, "test");
        WriteTag(spanMs, 6, WireType.Varint);
        WriteVarint(spanMs, 3);
        WriteTag(spanMs, 7, WireType.Fixed64);
        WriteFixed64(spanMs, 1_000_000_000);
        WriteTag(spanMs, 8, WireType.Fixed64);
        WriteFixed64(spanMs, 2_000_000_000);
        // Add ONE attribute
        WriteTag(spanMs, 9, WireType.LengthDelimited);
        WriteBytes(spanMs, BuildKeyValue("test.key", "test.value"));
        var spanBytes = spanMs.ToArray();

        using var ssMs = new MemoryStream();
        WriteTag(ssMs, 2, WireType.LengthDelimited);
        WriteBytes(ssMs, spanBytes);
        var ssBytes = ssMs.ToArray();

        using var rsMs = new MemoryStream();
        WriteTag(rsMs, 1, WireType.LengthDelimited);
        WriteBytes(rsMs, BuildResource("test-service"));
        WriteTag(rsMs, 2, WireType.LengthDelimited);
        WriteBytes(rsMs, ssBytes);
        var rsBytes = rsMs.ToArray();

        using var reqMs = new MemoryStream();
        WriteTag(reqMs, 1, WireType.LengthDelimited);
        WriteBytes(reqMs, rsBytes);
        var reqBytes = reqMs.ToArray();

        var request = OtlpProtobufParser.Parse(reqBytes.AsMemory());
        Assert.NotEmpty(request.ResourceSpans);
        Assert.NotEmpty(request.ResourceSpans[0].ScopeSpans);
        Assert.NotEmpty(request.ResourceSpans[0].ScopeSpans[0].Spans);
        var span = request.ResourceSpans[0].ScopeSpans[0].Spans[0];
        Assert.NotNull(span.Attributes);
        Assert.NotEmpty(span.Attributes);
        Assert.Equal("test.key", span.Attributes[0].Key);
    }

    [Fact]
    public void Parse_WithGenAiAttributes_ExtractsCorrectly()
    {
        // Arrange - Build payload with gen_ai.* attributes on the span
        var protoBytes = BuildExportTraceServiceRequest(
            BuildResourceSpans(
                BuildResource("ai-service"),
                BuildScopeSpans(
                    BuildSpanWithAttributes(
                        "0123456789abcdef0123456789abcdef", "0123456789abcdef", "chat completions",
                        kind: 3,
                        startNano: 1_000_000_000,
                        endNano: 2_000_000_000,
                        ("gen_ai.provider.name", "openai"),
                        ("gen_ai.request.model", "gpt-4o"),
                        ("gen_ai.response.model", "gpt-4o-2024-08-06"),
                        ("gen_ai.usage.input_tokens", "150"),
                        ("gen_ai.usage.output_tokens", "42"),
                        ("gen_ai.request.temperature", "0.7")))));

        // Act
        var request = OtlpProtobufParser.Parse(protoBytes.AsMemory());
        var rows = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        var row = Assert.Single(rows);
        Assert.Equal("openai", row.GenAiProviderName);
        Assert.Equal("gpt-4o", row.GenAiRequestModel);
        Assert.Equal("gpt-4o-2024-08-06", row.GenAiResponseModel);
        Assert.Equal(150L, row.GenAiInputTokens);
        Assert.Equal(42L, row.GenAiOutputTokens);
        Assert.Equal(0.7, row.GenAiTemperature);
    }

    [Fact]
    public void RoundTrip_ProtobufToStorageRow_PreservesData()
    {
        // Arrange
        const string traceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        const string spanId = "00f067aa0ba902b7";
        const string name = "GET /api/users";
        const int kind = 2; // SERVER
        const ulong startNano = 1_700_000_000_000_000_000;
        const ulong endNano = 1_700_000_001_500_000_000;

        var protoBytes = BuildExportTraceServiceRequest(
            BuildResourceSpans(
                BuildResource("my-service"),
                BuildScopeSpans(
                    BuildSpan(traceId, spanId, name, kind, startNano, endNano))));

        // Act
        var request = OtlpProtobufParser.Parse(protoBytes.AsMemory());
        var rows = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        var row = Assert.Single(rows);
        Assert.Equal(traceId, row.TraceId);
        Assert.Equal(spanId, row.SpanId);
        Assert.Equal(name, row.Name);
        Assert.Equal("my-service", row.ServiceName);
        Assert.Equal(kind, row.Kind);
        Assert.Equal(startNano, row.StartTimeUnixNano);
        Assert.Equal(endNano, row.EndTimeUnixNano);
    }

    // =========================================================================
    // Composable Protobuf Builders
    // =========================================================================
    // Uses the EXACT SAME wire format as qyl.collector.Grpc.ProtobufReader expects.
    // Each Build* method returns raw sub-message content bytes.
    // =========================================================================

    private static void WriteVarint(MemoryStream ms, ulong value)
    {
        while (value >= 0x80)
        {
            ms.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        ms.WriteByte((byte)value);
    }

    private static void WriteTag(MemoryStream ms, int fieldNumber, WireType wireType)
    {
        WriteVarint(ms, (ulong)((fieldNumber << 3) | (int)wireType));
    }

    private static void WriteBytes(MemoryStream ms, byte[] bytes)
    {
        WriteVarint(ms, (ulong)bytes.Length);
        ms.Write(bytes, 0, bytes.Length);
    }

    private static void WriteString(MemoryStream ms, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarint(ms, (ulong)bytes.Length);
        ms.Write(bytes, 0, bytes.Length);
    }

    private static void WriteFixed64(MemoryStream ms, ulong value)
    {
        var bytes = BitConverter.GetBytes(value);
        ms.Write(bytes, 0, 8);
    }

    /// <summary>Builds an AnyValue with string_value (field 1).</summary>
    private static byte[] BuildAnyValue(string value)
    {
        using var ms = new MemoryStream();
        WriteTag(ms, 1, WireType.LengthDelimited);
        WriteString(ms, value);
        return ms.ToArray();
    }

    /// <summary>Builds a KeyValue: key (field 1) + AnyValue (field 2).</summary>
    private static byte[] BuildKeyValue(string key, string value)
    {
        using var ms = new MemoryStream();
        WriteTag(ms, 1, WireType.LengthDelimited);
        WriteString(ms, key);
        WriteTag(ms, 2, WireType.LengthDelimited);
        WriteBytes(ms, BuildAnyValue(value));
        return ms.ToArray();
    }

    /// <summary>Builds a Resource with service.name and optional extra attributes.</summary>
    private static byte[] BuildResource(string serviceName, params (string key, string value)[] extraAttributes)
    {
        using var ms = new MemoryStream();
        // Resource.attributes = field 1, repeated
        WriteTag(ms, 1, WireType.LengthDelimited);
        WriteBytes(ms, BuildKeyValue("service.name", serviceName));
        foreach (var (key, value) in extraAttributes)
        {
            WriteTag(ms, 1, WireType.LengthDelimited);
            WriteBytes(ms, BuildKeyValue(key, value));
        }
        return ms.ToArray();
    }

    /// <summary>Builds a Span without attributes.</summary>
    private static byte[] BuildSpan(
        string traceId, string spanId, string name,
        int kind = 3, ulong startNano = 1_000_000_000, ulong endNano = 2_000_000_000)
    {
        using var ms = new MemoryStream();
        WriteTag(ms, 1, WireType.LengthDelimited);
        WriteBytes(ms, Convert.FromHexString(traceId));
        WriteTag(ms, 2, WireType.LengthDelimited);
        WriteBytes(ms, Convert.FromHexString(spanId));
        WriteTag(ms, 5, WireType.LengthDelimited);
        WriteString(ms, name);
        WriteTag(ms, 6, WireType.Varint);
        WriteVarint(ms, (ulong)kind);
        WriteTag(ms, 7, WireType.Fixed64);
        WriteFixed64(ms, startNano);
        WriteTag(ms, 8, WireType.Fixed64);
        WriteFixed64(ms, endNano);
        return ms.ToArray();
    }

    /// <summary>Builds a Span with attributes.</summary>
    private static byte[] BuildSpanWithAttributes(
        string traceId, string spanId, string name,
        int kind = 3, ulong startNano = 1_000_000_000, ulong endNano = 2_000_000_000,
        params (string key, string value)[] attributes)
    {
        using var ms = new MemoryStream();
        WriteTag(ms, 1, WireType.LengthDelimited);
        WriteBytes(ms, Convert.FromHexString(traceId));
        WriteTag(ms, 2, WireType.LengthDelimited);
        WriteBytes(ms, Convert.FromHexString(spanId));
        WriteTag(ms, 5, WireType.LengthDelimited);
        WriteString(ms, name);
        WriteTag(ms, 6, WireType.Varint);
        WriteVarint(ms, (ulong)kind);
        WriteTag(ms, 7, WireType.Fixed64);
        WriteFixed64(ms, startNano);
        WriteTag(ms, 8, WireType.Fixed64);
        WriteFixed64(ms, endNano);
        foreach (var (key, value) in attributes)
        {
            WriteTag(ms, 9, WireType.LengthDelimited);
            WriteBytes(ms, BuildKeyValue(key, value));
        }
        return ms.ToArray();
    }

    /// <summary>Builds ScopeSpans with spans (field 2).</summary>
    private static byte[] BuildScopeSpans(params byte[][] spans)
    {
        using var ms = new MemoryStream();
        foreach (var span in spans)
        {
            WriteTag(ms, 2, WireType.LengthDelimited);
            WriteBytes(ms, span);
        }
        return ms.ToArray();
    }

    /// <summary>Builds ResourceSpans: resource (field 1) + scope_spans (field 2).</summary>
    private static byte[] BuildResourceSpans(byte[] resource, byte[] scopeSpans)
    {
        using var ms = new MemoryStream();
        WriteTag(ms, 1, WireType.LengthDelimited);
        WriteBytes(ms, resource);
        WriteTag(ms, 2, WireType.LengthDelimited);
        WriteBytes(ms, scopeSpans);
        return ms.ToArray();
    }

    /// <summary>Builds an ExportTraceServiceRequest: resource_spans (field 1).</summary>
    private static byte[] BuildExportTraceServiceRequest(params byte[][] resourceSpans)
    {
        using var ms = new MemoryStream();
        foreach (var rs in resourceSpans)
        {
            WriteTag(ms, 1, WireType.LengthDelimited);
            WriteBytes(ms, rs);
        }
        return ms.ToArray();
    }
}
