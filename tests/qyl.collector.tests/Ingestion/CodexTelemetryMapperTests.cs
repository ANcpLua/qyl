using qyl.collector.Ingestion;
using qyl.collector.Storage;
using qyl.protocol.Attributes;

namespace qyl.collector.tests.Ingestion;

/// <summary>
///     Tests for CodexTelemetryMapper - transforms Codex custom telemetry to OTel GenAI.
/// </summary>
public sealed class CodexTelemetryMapperTests
{
    // =========================================================================
    // IsCodexSpan Tests
    // =========================================================================

    [Theory]
    [InlineData("codex.conversation_starts", true)]
    [InlineData("codex.api_request", true)]
    [InlineData("codex.sse_event", true)]
    [InlineData("codex.user_prompt", true)]
    [InlineData("codex.tool_decision", true)]
    [InlineData("codex.tool_result", true)]
    [InlineData("codex.custom_event", true)]
    [InlineData("chat gpt-4", false)]
    [InlineData("HTTP GET", false)]
    [InlineData(null, false)]
    public void IsCodexSpan_DetectsCodexPrefix(string? spanName, bool expected)
    {
        // Act
        var result = CodexTelemetryMapper.IsCodexSpan(spanName);

        // Assert
        Assert.Equal(expected, result);
    }

    // =========================================================================
    // HasCodexAttributes Tests
    // =========================================================================

    [Fact]
    public void HasCodexAttributes_WithCodexModel_ReturnsTrue()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.model"] = "gpt-4o"
        };

        // Act
        var result = CodexTelemetryMapper.HasCodexAttributes(attributes);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasCodexAttributes_WithCodexConversationId_ReturnsTrue()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.conversation_id"] = "conv-123"
        };

        // Act
        var result = CodexTelemetryMapper.HasCodexAttributes(attributes);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasCodexAttributes_WithoutCodexKeys_ReturnsFalse()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["gen_ai.provider.name"] = "openai",
            ["gen_ai.request.model"] = "gpt-4"
        };

        // Act
        var result = CodexTelemetryMapper.HasCodexAttributes(attributes);

        // Assert
        Assert.False(result);
    }

    // =========================================================================
    // TransformAttributes - Provider Mapping
    // =========================================================================

    [Fact]
    public void TransformAttributes_CodexSpan_SetsProviderToOpenAi()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.model"] = "gpt-4o"
        };

        // Act
        var transformed = CodexTelemetryMapper.TransformAttributes("codex.api_request", attributes);

        // Assert
        Assert.True(transformed);
        Assert.Equal(GenAiAttributes.Providers.OpenAi, attributes[GenAiAttributes.ProviderName]);
    }

    [Fact]
    public void TransformAttributes_ExistingProvider_NotOverwritten()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.model"] = "gpt-4o",
            [GenAiAttributes.ProviderName] = "azure.ai.openai"
        };

        // Act
        CodexTelemetryMapper.TransformAttributes("codex.api_request", attributes);

        // Assert - provider should NOT be overwritten
        Assert.Equal("azure.ai.openai", attributes[GenAiAttributes.ProviderName]);
    }

    // =========================================================================
    // TransformAttributes - Operation Name Mapping
    // =========================================================================

    [Theory]
    [InlineData("codex.conversation_starts", GenAiAttributes.Operations.Chat)]
    [InlineData("codex.api_request", GenAiAttributes.Operations.Chat)]
    [InlineData("codex.sse_event", GenAiAttributes.Operations.Chat)]
    [InlineData("codex.user_prompt", GenAiAttributes.Operations.Chat)]
    [InlineData("codex.tool_decision", GenAiAttributes.Operations.ExecuteTool)]
    [InlineData("codex.tool_result", GenAiAttributes.Operations.ExecuteTool)]
    public void TransformAttributes_MapsOperationName(string spanName, string expectedOperation)
    {
        // Arrange
        var attributes = new Dictionary<string, string>();

        // Act
        CodexTelemetryMapper.TransformAttributes(spanName, attributes);

        // Assert
        Assert.Equal(expectedOperation, attributes[GenAiAttributes.OperationName]);
    }

    // =========================================================================
    // TransformAttributes - Model Mapping
    // =========================================================================

    [Fact]
    public void TransformAttributes_CodexModel_MapsToRequestModel()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.model"] = "gpt-4o-2024-08-06"
        };

        // Act
        CodexTelemetryMapper.TransformAttributes("codex.api_request", attributes);

        // Assert
        Assert.Equal("gpt-4o-2024-08-06", attributes[GenAiAttributes.RequestModel]);
    }

    [Fact]
    public void TransformAttributes_SuccessfulRequest_AlsoSetsResponseModel()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.model"] = "gpt-4o",
            ["codex.success"] = "true"
        };

        // Act
        CodexTelemetryMapper.TransformAttributes("codex.api_request", attributes);

        // Assert
        Assert.Equal("gpt-4o", attributes[GenAiAttributes.RequestModel]);
        Assert.Equal("gpt-4o", attributes[GenAiAttributes.ResponseModel]);
    }

    [Fact]
    public void TransformAttributes_FailedRequest_DoesNotSetResponseModel()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.model"] = "gpt-4o",
            ["codex.success"] = "false"
        };

        // Act
        CodexTelemetryMapper.TransformAttributes("codex.api_request", attributes);

        // Assert
        Assert.Equal("gpt-4o", attributes[GenAiAttributes.RequestModel]);
        Assert.False(attributes.ContainsKey(GenAiAttributes.ResponseModel));
    }

    // =========================================================================
    // TransformAttributes - Conversation ID Mapping
    // =========================================================================

    [Fact]
    public void TransformAttributes_ConversationId_MapsToGenAi()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.conversation_id"] = "conv-abc123"
        };

        // Act
        CodexTelemetryMapper.TransformAttributes("codex.conversation_starts", attributes);

        // Assert
        Assert.Equal("conv-abc123", attributes[GenAiAttributes.ConversationId]);
    }

    [Fact]
    public void TransformAttributes_ThreadId_FallsBackToConversationId()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.thread_id"] = "thread-xyz789"
        };

        // Act
        CodexTelemetryMapper.TransformAttributes("codex.conversation_starts", attributes);

        // Assert
        Assert.Equal("thread-xyz789", attributes[GenAiAttributes.ConversationId]);
    }

    [Fact]
    public void TransformAttributes_ConversationIdTakesPrecedence_OverThreadId()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.conversation_id"] = "conv-primary",
            ["codex.thread_id"] = "thread-fallback"
        };

        // Act
        CodexTelemetryMapper.TransformAttributes("codex.conversation_starts", attributes);

        // Assert
        Assert.Equal("conv-primary", attributes[GenAiAttributes.ConversationId]);
    }

    // =========================================================================
    // TransformAttributes - Token Usage Mapping
    // =========================================================================

    [Fact]
    public void TransformAttributes_TokenCounts_MapsToGenAi()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.input_tokens"] = "150",
            ["codex.output_tokens"] = "350"
        };

        // Act
        CodexTelemetryMapper.TransformAttributes("codex.sse_event", attributes);

        // Assert
        Assert.Equal("150", attributes[GenAiAttributes.UsageInputTokens]);
        Assert.Equal("350", attributes[GenAiAttributes.UsageOutputTokens]);
    }

    // =========================================================================
    // TransformAttributes - Finish Reason Mapping
    // =========================================================================

    [Fact]
    public void TransformAttributes_FinishReason_MapsAsArray()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.finish_reason"] = "stop"
        };

        // Act
        CodexTelemetryMapper.TransformAttributes("codex.sse_event", attributes);

        // Assert
        Assert.Equal("[\"stop\"]", attributes[GenAiAttributes.ResponseFinishReasons]);
    }

    [Fact]
    public void TransformAttributes_FinishReason_ToolCalls()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.finish_reason"] = "tool_calls"
        };

        // Act
        CodexTelemetryMapper.TransformAttributes("codex.sse_event", attributes);

        // Assert
        Assert.Equal("[\"tool_calls\"]", attributes[GenAiAttributes.ResponseFinishReasons]);
    }

    // =========================================================================
    // TransformAttributes - Tool Attributes Mapping
    // =========================================================================

    [Fact]
    public void TransformAttributes_ToolName_MapsToGenAi()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.tool_name"] = "shell"
        };

        // Act
        CodexTelemetryMapper.TransformAttributes("codex.tool_result", attributes);

        // Assert
        Assert.Equal("shell", attributes[GenAiAttributes.ToolName]);
        Assert.Equal(GenAiAttributes.ToolTypes.Function, attributes[GenAiAttributes.ToolType]);
    }

    [Fact]
    public void TransformAttributes_ToolOutput_MapsToToolCallResult()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.tool_name"] = "read_file",
            ["codex.tool_output"] = "File content here..."
        };

        // Act
        CodexTelemetryMapper.TransformAttributes("codex.tool_result", attributes);

        // Assert
        Assert.Equal("File content here...", attributes[GenAiAttributes.ToolCallResult]);
    }

    // =========================================================================
    // TransformAttributes - Error Mapping
    // =========================================================================

    [Fact]
    public void TransformAttributes_ErrorType_MapsToGenAi()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.error_type"] = "rate_limit_exceeded"
        };

        // Act
        CodexTelemetryMapper.TransformAttributes("codex.api_request", attributes);

        // Assert
        Assert.Equal("rate_limit_exceeded", attributes[GenAiAttributes.ErrorType]);
    }

    [Fact]
    public void TransformAttributes_ErrorMessage_MapsToExceptionMessage()
    {
        // Arrange
        var attributes = new Dictionary<string, string>
        {
            ["codex.error_message"] = "Rate limit exceeded. Please retry after 60 seconds."
        };

        // Act
        CodexTelemetryMapper.TransformAttributes("codex.api_request", attributes);

        // Assert
        Assert.Equal("Rate limit exceeded. Please retry after 60 seconds.",
            attributes[GenAiAttributes.ExceptionMessage]);
    }

    // =========================================================================
    // TransformSpan Tests
    // =========================================================================

    [Fact]
    public void TransformSpan_CodexSpan_TransformsPromotedFields()
    {
        // Arrange
        var span = new SpanStorageRow
        {
            SpanId = "span-123",
            TraceId = "trace-456",
            Name = "codex.api_request",
            Kind = 3,
            StartTimeUnixNano = 1000000000UL,
            EndTimeUnixNano = 2000000000UL,
            DurationNs = 1000000000UL,
            StatusCode = 1,
            AttributesJson = """
            {
                "codex.model": "gpt-4o",
                "codex.input_tokens": "100",
                "codex.output_tokens": "200",
                "codex.conversation_id": "conv-abc",
                "codex.success": "true"
            }
            """
        };

        // Act
        var result = CodexTelemetryMapper.TransformSpan(span);

        // Assert
        Assert.Equal(GenAiAttributes.Providers.OpenAi, result.GenAiProviderName);
        Assert.Equal("gpt-4o", result.GenAiRequestModel);
        Assert.Equal("gpt-4o", result.GenAiResponseModel);
        Assert.Equal(100L, result.GenAiInputTokens);
        Assert.Equal(200L, result.GenAiOutputTokens);
    }

    [Fact]
    public void TransformSpan_NonCodexSpan_ReturnsUnchanged()
    {
        // Arrange
        var span = new SpanStorageRow
        {
            SpanId = "span-123",
            TraceId = "trace-456",
            Name = "HTTP GET /api/users",
            Kind = 3,
            StartTimeUnixNano = 1000000000UL,
            EndTimeUnixNano = 2000000000UL,
            DurationNs = 1000000000UL,
            StatusCode = 1,
            GenAiProviderName = null,
            AttributesJson = """{"http.method": "GET"}"""
        };

        // Act
        var result = CodexTelemetryMapper.TransformSpan(span);

        // Assert - should be unchanged
        Assert.Same(span, result);
    }

    // =========================================================================
    // TransformBatch Tests
    // =========================================================================

    [Fact]
    public void TransformBatch_MixedSpans_OnlyTransformsCodex()
    {
        // Arrange
        var spans = new List<SpanStorageRow>
        {
            CreateSpan("codex.api_request", """{"codex.model": "gpt-4o"}"""),
            CreateSpan("HTTP GET", """{"http.method": "GET"}"""),
            CreateSpan("codex.tool_result", """{"codex.tool_name": "shell"}""")
        };

        // Act
        var result = CodexTelemetryMapper.TransformBatch(spans);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(GenAiAttributes.Providers.OpenAi, result[0].GenAiProviderName);
        Assert.Null(result[1].GenAiProviderName); // Non-Codex span unchanged
        Assert.Equal("shell", result[2].GenAiToolName);
    }

    // =========================================================================
    // Extension Method Tests
    // =========================================================================

    [Fact]
    public void WithCodexTransformations_NoCodexSpans_ReturnsSameBatch()
    {
        // Arrange
        var spans = new List<SpanStorageRow>
        {
            CreateSpan("HTTP GET", """{"http.method": "GET"}""")
        };
        var batch = new SpanBatch(spans);

        // Act
        var result = batch.WithCodexTransformations();

        // Assert - should return original batch reference
        Assert.Same(batch, result);
    }

    [Fact]
    public void WithCodexTransformations_WithCodexSpans_ReturnsNewBatch()
    {
        // Arrange
        var spans = new List<SpanStorageRow>
        {
            CreateSpan("codex.api_request", """{"codex.model": "gpt-4o"}""")
        };
        var batch = new SpanBatch(spans);

        // Act
        var result = batch.WithCodexTransformations();

        // Assert - should return new batch with transformed spans
        Assert.NotSame(batch, result);
        Assert.Equal(GenAiAttributes.Providers.OpenAi, result.Spans[0].GenAiProviderName);
    }

    // =========================================================================
    // Helper Methods
    // =========================================================================

    private static SpanStorageRow CreateSpan(string name, string attributesJson)
    {
        return new SpanStorageRow
        {
            SpanId = Guid.NewGuid().ToString("N")[..16],
            TraceId = Guid.NewGuid().ToString("N"),
            Name = name,
            Kind = 3,
            StartTimeUnixNano = 1000000000UL,
            EndTimeUnixNano = 2000000000UL,
            DurationNs = 1000000000UL,
            StatusCode = 1,
            AttributesJson = attributesJson
        };
    }
}
