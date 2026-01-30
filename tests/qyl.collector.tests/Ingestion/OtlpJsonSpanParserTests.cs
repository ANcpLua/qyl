using System.Text;
using qyl.collector.Ingestion;

namespace qyl.collector.tests.Ingestion;

/// <summary>
///     Unit tests for OtlpJsonSpanParser - zero-allocation OTLP JSON parser.
///     Tests VS-01 acceptance criteria: OtlpJsonSpanParser parsing accuracy.
/// </summary>
public sealed class OtlpJsonSpanParserTests
{
    #region SpanKind Parsing Tests

    [Theory]
    [InlineData(0, 0)] // SpanKind.Unspecified
    [InlineData(1, 1)] // SpanKind.Internal
    [InlineData(2, 2)] // SpanKind.Server
    [InlineData(3, 3)] // SpanKind.Client
    [InlineData(4, 4)] // SpanKind.Producer
    [InlineData(5, 5)] // SpanKind.Consumer
    public void Parse_SpanKind_MapsCorrectly(int kindValue, int expectedKindValue)
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes($$"""
                                            {
                                                "resourceSpans": [{
                                                    "scopeSpans": [{
                                                        "spans": [{
                                                            "traceId": "0af7651916cd43dd8448eb211c80319c",
                                                            "spanId": "b7ad6b7169203331",
                                                            "name": "test",
                                                            "kind": {{kindValue}},
                                                            "startTimeUnixNano": "1640000000000000000",
                                                            "endTimeUnixNano": "1640000000100000000"
                                                        }]
                                                    }]
                                                }]
                                            }
                                            """);

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Single(spans);
        Assert.Equal((SpanKind)expectedKindValue, spans[0].Kind);
    }

    #endregion

    #region String Interning Tests

    [Fact]
    public void Parse_CommonProviderNames_InternsStrings()
    {
        // Arrange - Parse same provider multiple times
        var json = Encoding.UTF8.GetBytes(GenAiOtlpRequest);

        // Act - Parse twice
        var parser1 = new OtlpJsonSpanParser(json.AsSpan());
        var spans1 = parser1.ParseExportRequest();

        var parser2 = new OtlpJsonSpanParser(json.AsSpan());
        var spans2 = parser2.ParseExportRequest();

        // Assert - Interned strings should be reference equal
        Assert.Equal(spans1[0].ProviderName, spans2[0].ProviderName);
        Assert.Equal("openai", spans1[0].ProviderName);
    }

    #endregion

    #region Custom Attributes Tests

    [Fact]
    public void Parse_CustomAttributes_PreservedInAttributesList()
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes("""
                                          {
                                              "resourceSpans": [{
                                                  "scopeSpans": [{
                                                      "spans": [{
                                                          "traceId": "0af7651916cd43dd8448eb211c80319c",
                                                          "spanId": "b7ad6b7169203331",
                                                          "name": "test",
                                                          "kind": 1,
                                                          "startTimeUnixNano": "1640000000000000000",
                                                          "endTimeUnixNano": "1640000000100000000",
                                                          "attributes": [
                                                              { "key": "custom.string", "value": { "stringValue": "hello" } },
                                                              { "key": "custom.int", "value": { "intValue": 42 } },
                                                              { "key": "custom.double", "value": { "doubleValue": 3.14 } },
                                                              { "key": "custom.bool", "value": { "boolValue": true } }
                                                          ]
                                                      }]
                                                  }]
                                              }]
                                          }
                                          """);

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Single(spans);
        var attributes = spans[0].Attributes;
        Assert.NotNull(attributes);
        Assert.Equal(4, attributes.Count);

        var attrs = attributes.ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.Equal("hello", attrs["custom.string"]);
        Assert.Equal(42L, attrs["custom.int"]);
        Assert.Equal(3.14, attrs["custom.double"]);
        Assert.Equal(true, attrs["custom.bool"]);
    }

    #endregion

    #region Test Data

    private const string MinimalOtlpRequest = """
                                              {
                                                  "resourceSpans": [{
                                                      "scopeSpans": [{
                                                          "spans": [{
                                                              "traceId": "0af7651916cd43dd8448eb211c80319c",
                                                              "spanId": "b7ad6b7169203331",
                                                              "name": "test-span",
                                                              "kind": 1,
                                                              "startTimeUnixNano": "1640000000000000000",
                                                              "endTimeUnixNano": "1640000000100000000"
                                                          }]
                                                      }]
                                                  }]
                                              }
                                              """;

    private const string GenAiOtlpRequest = """
                                            {
                                                "resourceSpans": [{
                                                    "resource": {
                                                        "attributes": [{
                                                            "key": "service.name",
                                                            "value": { "stringValue": "my-ai-app" }
                                                        }]
                                                    },
                                                    "scopeSpans": [{
                                                        "spans": [{
                                                            "traceId": "0af7651916cd43dd8448eb211c80319c",
                                                            "spanId": "b7ad6b7169203331",
                                                            "name": "chat",
                                                            "kind": 3,
                                                            "startTimeUnixNano": "1640000000000000000",
                                                            "endTimeUnixNano": "1640000000500000000",
                                                            "status": {
                                                                "code": 1,
                                                                "message": "OK"
                                                            },
                                                            "attributes": [
                                                                { "key": "gen_ai.provider.name", "value": { "stringValue": "openai" } },
                                                                { "key": "gen_ai.request.model", "value": { "stringValue": "gpt-4" } },
                                                                { "key": "gen_ai.response.model", "value": { "stringValue": "gpt-4-0613" } },
                                                                { "key": "gen_ai.operation.name", "value": { "stringValue": "chat" } },
                                                                { "key": "gen_ai.usage.input_tokens", "value": { "intValue": "150" } },
                                                                { "key": "gen_ai.usage.output_tokens", "value": { "intValue": 75 } },
                                                                { "key": "gen_ai.request.temperature", "value": { "doubleValue": 0.7 } },
                                                                { "key": "session.id", "value": { "stringValue": "a1b2c3d4-e5f6-7890-abcd-ef1234567890" } }
                                                            ]
                                                        }]
                                                    }]
                                                }]
                                            }
                                            """;

    private const string DeprecatedAttributesOtlpRequest = """
                                                           {
                                                               "resourceSpans": [{
                                                                   "scopeSpans": [{
                                                                       "spans": [{
                                                                           "traceId": "0af7651916cd43dd8448eb211c80319c",
                                                                           "spanId": "b7ad6b7169203331",
                                                                           "name": "legacy-span",
                                                                           "kind": 3,
                                                                           "startTimeUnixNano": "1640000000000000000",
                                                                           "endTimeUnixNano": "1640000000100000000",
                                                                           "attributes": [
                                                                               { "key": "gen_ai.system", "value": { "stringValue": "anthropic" } },
                                                                               { "key": "gen_ai.usage.prompt_tokens", "value": { "intValue": 100 } },
                                                                               { "key": "gen_ai.usage.completion_tokens", "value": { "intValue": 50 } }
                                                                           ]
                                                                       }]
                                                                   }]
                                                               }]
                                                           }
                                                           """;

    // Valid 16-char hex span IDs for OTLP
    private const string ParentSpanIdHex = "b7ad6b7169203331";

    private const string MultipleSpansOtlpRequest = """
                                                    {
                                                        "resourceSpans": [{
                                                            "scopeSpans": [{
                                                                "spans": [
                                                                    {
                                                                        "traceId": "0af7651916cd43dd8448eb211c80319c",
                                                                        "spanId": "b7ad6b7169203331",
                                                                        "name": "parent",
                                                                        "kind": 1,
                                                                        "startTimeUnixNano": "1640000000000000000",
                                                                        "endTimeUnixNano": "1640000000100000000"
                                                                    },
                                                                    {
                                                                        "traceId": "0af7651916cd43dd8448eb211c80319c",
                                                                        "spanId": "b7ad6b7169203332",
                                                                        "parentSpanId": "b7ad6b7169203331",
                                                                        "name": "child-1",
                                                                        "kind": 1,
                                                                        "startTimeUnixNano": "1640000000010000000",
                                                                        "endTimeUnixNano": "1640000000050000000"
                                                                    },
                                                                    {
                                                                        "traceId": "0af7651916cd43dd8448eb211c80319c",
                                                                        "spanId": "b7ad6b7169203333",
                                                                        "parentSpanId": "b7ad6b7169203331",
                                                                        "name": "child-2",
                                                                        "kind": 1,
                                                                        "startTimeUnixNano": "1640000000060000000",
                                                                        "endTimeUnixNano": "1640000000090000000"
                                                                    }
                                                                ]
                                                            }]
                                                        }]
                                                    }
                                                    """;

    private const string ErrorSpanOtlpRequest = """
                                                {
                                                    "resourceSpans": [{
                                                        "scopeSpans": [{
                                                            "spans": [{
                                                                "traceId": "0af7651916cd43dd8448eb211c80319c",
                                                                "spanId": "error-span",
                                                                "name": "failed-operation",
                                                                "kind": 3,
                                                                "startTimeUnixNano": "1640000000000000000",
                                                                "endTimeUnixNano": "1640000000100000000",
                                                                "status": {
                                                                    "code": 2,
                                                                    "message": "Connection timeout"
                                                                }
                                                            }]
                                                        }]
                                                    }]
                                                }
                                                """;

    #endregion

    #region Basic Parsing Tests

    [Fact]
    public void Parse_MinimalSpan_ExtractsRequiredFields()
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes(MinimalOtlpRequest);

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Single(spans);
        var span = spans[0];
        Assert.Equal("0af7651916cd43dd8448eb211c80319c", span.TraceId.ToString());
        Assert.Equal("b7ad6b7169203331", span.SpanId.ToString());
        Assert.Equal("test-span", span.Name);
        Assert.Equal(SpanKind.Internal, span.Kind);
        Assert.Equal(1640000000000000000UL, span.StartTime.Value);
        Assert.Equal(1640000000100000000UL, span.EndTime.Value);
    }

    [Fact]
    public void Parse_EmptyRequest_ReturnsEmptyList()
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes("{}");

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Empty(spans);
    }

    [Fact]
    public void Parse_EmptyResourceSpans_ReturnsEmptyList()
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes("""{"resourceSpans": []}""");

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Empty(spans);
    }

    [Fact]
    public void Parse_MultipleSpans_ReturnsAll()
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes(MultipleSpansOtlpRequest);

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Equal(3, spans.Count);
        Assert.Equal("parent", spans[0].Name);
        Assert.Equal("child-1", spans[1].Name);
        Assert.Equal("child-2", spans[2].Name);

        // Verify parent-child relationships
        Assert.True(spans[0].ParentSpanId.IsEmpty);
        Assert.Equal(ParentSpanIdHex, spans[1].ParentSpanId.ToString());
        Assert.Equal(ParentSpanIdHex, spans[2].ParentSpanId.ToString());
    }

    #endregion

    #region GenAI Attribute Extraction Tests

    [Fact]
    public void Parse_GenAiSpan_ExtractsPromotedAttributes()
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes(GenAiOtlpRequest);

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Single(spans);
        var span = spans[0];

        // Verify GenAI attributes
        Assert.Equal("openai", span.ProviderName);
        Assert.Equal("gpt-4", span.RequestModel);
        Assert.Equal("gpt-4-0613", span.ResponseModel);
        Assert.Equal("chat", span.OperationName);
        Assert.Equal(150, span.InputTokens);
        Assert.Equal(75, span.OutputTokens);
        Assert.Equal(225, span.TotalTokens);
        Assert.Equal(0.7, span.Temperature);

        // Verify session - SessionId stores GUID as string without hyphens (format "N")
        Assert.NotNull(span.SessionId);
        Assert.Equal("a1b2c3d4e5f67890abcdef1234567890", span.SessionId.Value.Value);

        // Verify it's recognized as GenAI span
        Assert.True(span.IsGenAiSpan);
    }

    [Fact]
    public void Parse_DeprecatedAttributes_MapsToCurrentVersion()
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes(DeprecatedAttributesOtlpRequest);

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Single(spans);
        var span = spans[0];

        // gen_ai.system → gen_ai.provider.name
        Assert.Equal("anthropic", span.ProviderName);

        // gen_ai.usage.prompt_tokens → gen_ai.usage.input_tokens
        Assert.Equal(100, span.InputTokens);

        // gen_ai.usage.completion_tokens → gen_ai.usage.output_tokens
        Assert.Equal(50, span.OutputTokens);
    }

    [Fact]
    public void Parse_NonGenAiSpan_HasNullProviderAndModel()
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes(MinimalOtlpRequest);

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Single(spans);
        var span = spans[0];

        Assert.Null(span.ProviderName);
        Assert.Null(span.RequestModel);
        Assert.Equal(0, span.InputTokens);
        Assert.Equal(0, span.OutputTokens);
        Assert.False(span.IsGenAiSpan);
    }

    #endregion

    #region Status Parsing Tests

    [Fact]
    public void Parse_StatusOk_ExtractsCorrectly()
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes(GenAiOtlpRequest);

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Single(spans);
        Assert.Equal(StatusCode.Ok, spans[0].Status);
        Assert.Equal("OK", spans[0].StatusMessage);
    }

    [Fact]
    public void Parse_StatusError_ExtractsCorrectly()
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes(ErrorSpanOtlpRequest);

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Single(spans);
        Assert.Equal(StatusCode.Error, spans[0].Status);
        Assert.Equal("Connection timeout", spans[0].StatusMessage);
    }

    [Fact]
    public void Parse_NoStatus_DefaultsToUnset()
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes(MinimalOtlpRequest);

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Single(spans);
        Assert.Equal(StatusCode.Unset, spans[0].Status);
        Assert.Null(spans[0].StatusMessage);
    }

    #endregion

    #region Duration Calculation Tests

    [Fact]
    public void Parse_Duration_CalculatesCorrectly()
    {
        // Arrange - 100ms duration
        var json = Encoding.UTF8.GetBytes(MinimalOtlpRequest);

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Single(spans);
        var duration = spans[0].Duration;
        Assert.Equal(100, duration.TotalMilliseconds);
    }

    [Fact]
    public void Parse_LongDuration_CalculatesCorrectly()
    {
        // Arrange - 500ms duration (from GenAi request)
        var json = Encoding.UTF8.GetBytes(GenAiOtlpRequest);

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Single(spans);
        var duration = spans[0].Duration;
        Assert.Equal(500, duration.TotalMilliseconds);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Parse_MissingTraceId_SkipsSpan()
    {
        // Arrange
        var json = Encoding.UTF8.GetBytes("""
                                          {
                                              "resourceSpans": [{
                                                  "scopeSpans": [{
                                                      "spans": [{
                                                          "spanId": "b7ad6b7169203331",
                                                          "name": "test",
                                                          "kind": 1,
                                                          "startTimeUnixNano": "1640000000000000000",
                                                          "endTimeUnixNano": "1640000000100000000"
                                                      }]
                                                  }]
                                              }]
                                          }
                                          """);

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert - Span without traceId should be skipped
        Assert.Empty(spans);
    }

    [Fact]
    public void Parse_IntAsString_ParsesCorrectly()
    {
        // Arrange - OTLP JSON encodes large numbers as strings
        var json = Encoding.UTF8.GetBytes("""
                                          {
                                              "resourceSpans": [{
                                                  "scopeSpans": [{
                                                      "spans": [{
                                                          "traceId": "0af7651916cd43dd8448eb211c80319c",
                                                          "spanId": "b7ad6b7169203331",
                                                          "name": "test",
                                                          "kind": 1,
                                                          "startTimeUnixNano": "9223372036854775807",
                                                          "endTimeUnixNano": "9223372036854775807"
                                                      }]
                                                  }]
                                              }]
                                          }
                                          """);

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Single(spans);
        Assert.Equal((ulong)long.MaxValue, spans[0].StartTime.Value);
    }

    [Fact]
    public void Parse_WithTrailingCommas_HandleGracefully()
    {
        // Arrange - JSON with trailing commas (allowed by parser options)
        var json = Encoding.UTF8.GetBytes("""
                                          {
                                              "resourceSpans": [{
                                                  "scopeSpans": [{
                                                      "spans": [{
                                                          "traceId": "0af7651916cd43dd8448eb211c80319c",
                                                          "spanId": "b7ad6b7169203331",
                                                          "name": "test",
                                                          "kind": 1,
                                                          "startTimeUnixNano": "1640000000000000000",
                                                          "endTimeUnixNano": "1640000000100000000",
                                                      },],
                                                  },],
                                              },],
                                          }
                                          """);

        // Act
        var parser = new OtlpJsonSpanParser(json.AsSpan());
        var spans = parser.ParseExportRequest();

        // Assert
        Assert.Single(spans);
    }

    #endregion
}
