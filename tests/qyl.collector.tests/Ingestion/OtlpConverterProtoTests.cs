using qyl.collector.Grpc;
using qyl.collector.Ingestion;

namespace qyl.collector.tests.Ingestion;

/// <summary>
///     Tests for OtlpConverter.ConvertProtoToStorageRows (gRPC/Proto path).
///     Addresses TEST-002 coverage gap: Proto/gRPC path has 0% test coverage.
/// </summary>
public sealed class OtlpConverterProtoTests
{
    // =========================================================================
    // Basic Conversion Tests
    // =========================================================================

    [Fact]
    public void ConvertProtoToStorageRows_ValidRequest_ReturnsSpans()
    {
        // Arrange
        var request = CreateProtoRequest(
            traceId: "4bf92f3577b34da6a3ce929d0e0e4736",
            spanId: "00f067aa0ba902b7",
            name: "test-span",
            serviceName: "test-service");

        // Act
        var spans = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        Assert.Single(spans);
        var span = spans[0];
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", span.TraceId);
        Assert.Equal("00f067aa0ba902b7", span.SpanId);
        Assert.Equal("test-span", span.Name);
        Assert.Equal("test-service", span.ServiceName);
    }

    [Fact]
    public void ConvertProtoToStorageRows_EmptyResourceSpans_ReturnsEmpty()
    {
        // Arrange
        var request = new ExportTraceServiceRequest();

        // Act
        var spans = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        Assert.Empty(spans);
    }

    [Fact]
    public void ConvertProtoToStorageRows_MultipleSpans_ReturnsAll()
    {
        // Arrange
        var request = new ExportTraceServiceRequest();
        var resourceSpan = CreateResourceSpans("test-service");

        // Add multiple spans
        resourceSpan.ScopeSpans[0].Spans.Add(CreateSpan("trace1", "span1", "op1"));
        resourceSpan.ScopeSpans[0].Spans.Add(CreateSpan("trace1", "span2", "op2"));
        resourceSpan.ScopeSpans[0].Spans.Add(CreateSpan("trace2", "span3", "op3"));

        request.ResourceSpans.Add(resourceSpan);

        // Act
        var spans = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        Assert.Equal(3, spans.Count);
        Assert.Equal("span1", spans[0].SpanId);
        Assert.Equal("span2", spans[1].SpanId);
        Assert.Equal("span3", spans[2].SpanId);
    }

    // =========================================================================
    // GenAI Attribute Extraction Tests
    // =========================================================================

    [Fact]
    public void ConvertProtoToStorageRows_WithGenAiAttributes_ExtractsPromoted()
    {
        // Arrange
        var request = CreateProtoRequestWithGenAi(
            providerName: "openai",
            requestModel: "gpt-4",
            responseModel: "gpt-4-0613",
            inputTokens: 100,
            outputTokens: 200);

        // Act
        var spans = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        Assert.Single(spans);
        var span = spans[0];
        Assert.Equal("openai", span.GenAiProviderName);
        Assert.Equal("gpt-4", span.GenAiRequestModel);
        Assert.Equal("gpt-4-0613", span.GenAiResponseModel);
        Assert.Equal(100L, span.GenAiInputTokens);
        Assert.Equal(200L, span.GenAiOutputTokens);
    }

    [Fact]
    public void ConvertProtoToStorageRows_WithFinishReasons_ExtractsStopReason()
    {
        // Arrange - test the fix: finish_reasons (plural) should be extracted
        var request = CreateProtoRequest("trace", "span", "chat");
        var protoSpan = request.ResourceSpans[0].ScopeSpans[0].Spans[0]!;
        protoSpan.Attributes!.Add(new OtlpKeyValueProto
        {
            Key = "gen_ai.response.finish_reasons",
            Value = new OtlpAnyValueProto
            {
                ArrayValue =
                [
                    new OtlpAnyValueProto { StringValue = "stop" }
                ]
            }
        });

        // Act
        var spans = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        var span = spans[0];
        Assert.Equal("[\"stop\"]", span.GenAiStopReason);
    }

    [Fact]
    public void ConvertProtoToStorageRows_WithDeprecatedFinishReason_FallsBack()
    {
        // Arrange - test backward compat: singular finish_reason should still work
        var request = CreateProtoRequest("trace", "span", "chat");
        var protoSpan = request.ResourceSpans[0].ScopeSpans[0].Spans[0]!;
        protoSpan.Attributes!.Add(new OtlpKeyValueProto
        {
            Key = "gen_ai.response.finish_reason",
            Value = new OtlpAnyValueProto { StringValue = "stop" }
        });

        // Act
        var spans = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        var span = spans[0];
        Assert.Equal("stop", span.GenAiStopReason);
    }

    [Fact]
    public void ConvertProtoToStorageRows_WithDeprecatedGenAiProviderName_ExtractsProviderName()
    {
        // Arrange - test deprecated attribute fallback
        var request = CreateProtoRequest("trace", "span", "chat");
        var protoSpan = request.ResourceSpans[0].ScopeSpans[0].Spans[0]!;
        protoSpan.Attributes!.Add(new OtlpKeyValueProto
        {
            Key = "gen_ai.system",  // deprecated
            Value = new OtlpAnyValueProto { StringValue = "anthropic" }
        });

        // Act
        var spans = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        var span = spans[0];
        Assert.Equal("anthropic", span.GenAiProviderName);
    }

    [Fact]
    public void ConvertProtoToStorageRows_WithDeprecatedPromptTokens_ExtractsInputTokens()
    {
        // Arrange - test deprecated token attribute fallback
        var request = CreateProtoRequest("trace", "span", "chat");
        var protoSpan = request.ResourceSpans[0].ScopeSpans[0].Spans[0]!;
        protoSpan.Attributes!.Add(new OtlpKeyValueProto
        {
            Key = "gen_ai.usage.prompt_tokens",  // deprecated
            Value = new OtlpAnyValueProto { IntValue = 150 }
        });

        // Act
        var spans = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        var span = spans[0];
        Assert.Equal(150L, span.GenAiInputTokens);
    }

    // =========================================================================
    // SpanKind Tests
    // =========================================================================

    [Theory]
    [InlineData(0, 0)] // UNSPECIFIED -> UNSPECIFIED
    [InlineData(1, 1)] // INTERNAL -> INTERNAL
    [InlineData(2, 2)] // SERVER -> SERVER
    [InlineData(3, 3)] // CLIENT -> CLIENT
    [InlineData(4, 4)] // PRODUCER -> PRODUCER
    [InlineData(5, 5)] // CONSUMER -> CONSUMER
    public void ConvertProtoToStorageRows_SpanKind_MapsCorrectly(int protoKind, byte expectedKind)
    {
        // Arrange
        var request = CreateProtoRequest("trace", "span", "op");
        request.ResourceSpans[0].ScopeSpans[0].Spans[0].Kind = protoKind;

        // Act
        var spans = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        Assert.Equal(expectedKind, spans[0].Kind);
    }

    // =========================================================================
    // StatusCode Tests
    // =========================================================================

    [Theory]
    [InlineData(0, 0)] // UNSET
    [InlineData(1, 1)] // OK
    [InlineData(2, 2)] // ERROR
    public void ConvertProtoToStorageRows_StatusCode_MapsCorrectly(int protoStatus, byte expectedStatus)
    {
        // Arrange
        var request = CreateProtoRequest("trace", "span", "op");
        request.ResourceSpans[0].ScopeSpans[0].Spans[0].Status = new OtlpStatusProto
        {
            Code = protoStatus,
            Message = protoStatus == 2 ? "Error occurred" : null
        };

        // Act
        var spans = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        Assert.Equal(expectedStatus, spans[0].StatusCode);
        if (protoStatus == 2)
            Assert.Equal("Error occurred", spans[0].StatusMessage);
    }

    // =========================================================================
    // Edge Cases
    // =========================================================================

    [Fact]
    public void ConvertProtoToStorageRows_MissingServiceName_DefaultsToUnknown()
    {
        // Arrange
        var request = new ExportTraceServiceRequest();
        var resourceSpan = new OtlpResourceSpansProto();
        var scopeSpan = new OtlpScopeSpansProto();
        scopeSpan.Spans.Add(CreateSpan("trace", "span", "op"));
        resourceSpan.ScopeSpans.Add(scopeSpan);
        request.ResourceSpans.Add(resourceSpan);

        // Act
        var spans = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        Assert.Equal("unknown", spans[0].ServiceName);
    }

    [Fact]
    public void ConvertProtoToStorageRows_NullAttributes_DoesNotThrow()
    {
        // Arrange
        var request = new ExportTraceServiceRequest();
        var resourceSpan = CreateResourceSpans("test-service");
        var span = new OtlpSpanProto
        {
            TraceId = "4bf92f3577b34da6a3ce929d0e0e4736",
            SpanId = "00f067aa0ba902b7",
            Name = "test",
            StartTimeUnixNano = 1000000000UL,
            EndTimeUnixNano = 2000000000UL
            // Attributes is null/empty
        };
        resourceSpan.ScopeSpans[0].Spans.Add(span);
        request.ResourceSpans.Add(resourceSpan);

        // Act
        var spans = OtlpConverter.ConvertProtoToStorageRows(request);

        // Assert
        Assert.Single(spans);
    }

    // =========================================================================
    // Helper Methods
    // =========================================================================

    private static ExportTraceServiceRequest CreateProtoRequest(
        string traceId,
        string spanId,
        string name,
        string serviceName = "test-service")
    {
        var request = new ExportTraceServiceRequest();
        var resourceSpan = CreateResourceSpans(serviceName);
        resourceSpan.ScopeSpans[0].Spans.Add(CreateSpan(traceId, spanId, name));
        request.ResourceSpans.Add(resourceSpan);
        return request;
    }

    private static ExportTraceServiceRequest CreateProtoRequestWithGenAi(
        string providerName,
        string requestModel,
        string responseModel,
        long inputTokens,
        long outputTokens)
    {
        var request = CreateProtoRequest("trace001", "span001", "chat gpt-4", "genai-service");
        var protoSpan = request.ResourceSpans[0].ScopeSpans[0].Spans[0]!;

        protoSpan.Attributes!.Add(new OtlpKeyValueProto
        {
            Key = "gen_ai.provider.name",
            Value = new OtlpAnyValueProto { StringValue = providerName }
        });
        protoSpan.Attributes!.Add(new OtlpKeyValueProto
        {
            Key = "gen_ai.request.model",
            Value = new OtlpAnyValueProto { StringValue = requestModel }
        });
        protoSpan.Attributes!.Add(new OtlpKeyValueProto
        {
            Key = "gen_ai.response.model",
            Value = new OtlpAnyValueProto { StringValue = responseModel }
        });
        protoSpan.Attributes!.Add(new OtlpKeyValueProto
        {
            Key = "gen_ai.usage.input_tokens",
            Value = new OtlpAnyValueProto { IntValue = inputTokens }
        });
        protoSpan.Attributes!.Add(new OtlpKeyValueProto
        {
            Key = "gen_ai.usage.output_tokens",
            Value = new OtlpAnyValueProto { IntValue = outputTokens }
        });

        return request;
    }

    private static OtlpResourceSpansProto CreateResourceSpans(string serviceName)
    {
        var resourceSpan = new OtlpResourceSpansProto
        {
            Resource = new OtlpResourceProto()
        };
        resourceSpan.Resource.Attributes.Add(new OtlpKeyValueProto
        {
            Key = "service.name",
            Value = new OtlpAnyValueProto { StringValue = serviceName }
        });
        resourceSpan.ScopeSpans.Add(new OtlpScopeSpansProto());
        return resourceSpan;
    }

    private static OtlpSpanProto CreateSpan(string traceId, string spanId, string name)
    {
        return new OtlpSpanProto
        {
            TraceId = traceId,
            SpanId = spanId,
            Name = name,
            Kind = 3, // CLIENT
            StartTimeUnixNano = 1000000000UL,
            EndTimeUnixNano = 2000000000UL,
            Attributes = [] // Initialize to allow adding attributes in tests
        };
    }
}
