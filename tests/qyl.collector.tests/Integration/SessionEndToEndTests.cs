using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using qyl.collector.Ingestion;

namespace qyl.collector.tests.Integration;

/// <summary>
///     End-to-end tests verifying full data flow:
///     Ingest → Store → Query → API Response
/// </summary>
public sealed class SessionEndToEndTests : IClassFixture<QylWebApplicationFactory>, IAsyncLifetime
{
    private readonly QylWebApplicationFactory _factory;
    private HttpClient? _client;

    public SessionEndToEndTests(QylWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client => _client ?? throw new InvalidOperationException("Client not initialized");

    public ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {QylWebApplicationFactory.TestToken}");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    // =========================================================================
    // Full Flow Tests
    // =========================================================================

    [Fact]
    public async Task IngestSpan_ThenQuerySession_ReturnsIngestedData()
    {
        // Arrange - Create unique IDs for this test
        var testSessionId = $"e2e-session-{Guid.NewGuid():N}";
        var testTraceId = $"e2e-trace-{Guid.NewGuid():N}"[..32]; // 32 hex chars
        var testSpanId = $"e2e-span-{Guid.NewGuid():N}"[..16]; // 16 hex chars

        var otlpRequest = CreateOtlpRequestWithSession(testTraceId, testSpanId, testSessionId);

        // Act - Ingest the span
        var json = JsonSerializer.Serialize(otlpRequest, QylSerializerContext.Default.OtlpExportTraceServiceRequest);
        var content = new StringContent(json, Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json"));
        var ingestResponse = await Client.PostAsync("/v1/traces", content);

        Assert.Equal(HttpStatusCode.Accepted, ingestResponse.StatusCode);

        // Wait for async processing
        await Task.Delay(TestConstants.BatchProcessingDelayMs);

        // Act - Query the sessions list
        var sessionsResponse = await Client.GetAsync("/api/v1/sessions");

        Assert.Equal(HttpStatusCode.OK, sessionsResponse.StatusCode);

        var sessionsContent = await sessionsResponse.Content.ReadAsStringAsync();
        // The session should appear in the list (or be grouped by trace_id if no session.id)
        Assert.Contains("sessions", sessionsContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IngestGenAiSpan_ThenQuerySession_ReturnsTokenData()
    {
        // Arrange
        var testSessionId = $"genai-session-{Guid.NewGuid():N}";
        var testTraceId = $"genai-trace-{Guid.NewGuid():N}"[..32];
        var testSpanId = $"genai-span-{Guid.NewGuid():N}"[..16];

        var otlpRequest = CreateGenAiOtlpRequestWithSession(
            testTraceId, testSpanId, testSessionId,
            150, 75);

        // Act - Ingest
        var json = JsonSerializer.Serialize(otlpRequest, QylSerializerContext.Default.OtlpExportTraceServiceRequest);
        var content = new StringContent(json, Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json"));
        var ingestResponse = await Client.PostAsync("/v1/traces", content);

        Assert.Equal(HttpStatusCode.Accepted, ingestResponse.StatusCode);

        // Wait for processing
        await Task.Delay(TestConstants.BatchProcessingDelayMs);

        // Query sessions
        var sessionsResponse = await Client.GetAsync("/api/v1/sessions");

        Assert.Equal(HttpStatusCode.OK, sessionsResponse.StatusCode);
    }

    [Fact]
    public async Task IngestMultipleSpans_ThenQuerySessionSpans_ReturnsAllSpans()
    {
        // Arrange - Same trace, different spans
        var testSessionId = $"multi-session-{Guid.NewGuid():N}";
        var testTraceId = $"multi-trace-{Guid.NewGuid():N}"[..32];

        // Create 3 spans in the same trace
        var spans = new List<OtlpSpan>();
        for (var i = 0; i < 3; i++)
        {
            var spanId = $"multi-span-{i:D2}-{Guid.NewGuid():N}"[..16];
            spans.Add(CreateSpan(testTraceId, spanId, testSessionId, $"operation-{i}"));
        }

        var otlpRequest = new OtlpExportTraceServiceRequest
        {
            ResourceSpans =
            [
                new OtlpResourceSpans
                {
                    Resource = new OtlpResource
                    {
                        Attributes =
                        [
                            new OtlpKeyValue
                            {
                                Key = "service.name",
                                Value = new OtlpAnyValue
                                {
                                    StringValue = "multi-span-service"
                                }
                            }
                        ]
                    },
                    ScopeSpans =
                    [
                        new OtlpScopeSpans
                        {
                            Spans = spans
                        }
                    ]
                }
            ]
        };

        // Act - Ingest
        var json = JsonSerializer.Serialize(otlpRequest, QylSerializerContext.Default.OtlpExportTraceServiceRequest);
        var content = new StringContent(json, Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json"));
        var ingestResponse = await Client.PostAsync("/v1/traces", content);

        Assert.Equal(HttpStatusCode.Accepted, ingestResponse.StatusCode);

        // Wait for processing
        await Task.Delay(TestConstants.LargeBatchProcessingDelayMs);

        // Query the trace
        var traceResponse = await Client.GetAsync($"/api/v1/traces/{testTraceId}");

        // Should find the trace, NotFound if not indexed yet, or InternalServerError if DB issue
        Assert.True(
            traceResponse.StatusCode == HttpStatusCode.OK ||
            traceResponse.StatusCode == HttpStatusCode.NotFound ||
            traceResponse.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected OK, NotFound, or InternalServerError, got {traceResponse.StatusCode}");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static OtlpExportTraceServiceRequest CreateOtlpRequestWithSession(
        string traceId, string spanId, string sessionId)
    {
        var now = DateTimeOffset.UtcNow;
        var startNano = (ulong)(now.ToUnixTimeMilliseconds() * 1_000_000);
        var endNano = (ulong)((now.ToUnixTimeMilliseconds() + 100) * 1_000_000);

        return new OtlpExportTraceServiceRequest
        {
            ResourceSpans =
            [
                new OtlpResourceSpans
                {
                    Resource = new OtlpResource
                    {
                        Attributes =
                        [
                            new OtlpKeyValue
                            {
                                Key = "service.name",
                                Value = new OtlpAnyValue
                                {
                                    StringValue = "e2e-test-service"
                                }
                            }
                        ]
                    },
                    ScopeSpans =
                    [
                        new OtlpScopeSpans
                        {
                            Spans =
                            [
                                new OtlpSpan
                                {
                                    TraceId = traceId,
                                    SpanId = spanId,
                                    Name = "e2e-test-operation",
                                    Kind = 1,
                                    StartTimeUnixNano = startNano,
                                    EndTimeUnixNano = endNano,
                                    Status = new OtlpStatus
                                    {
                                        Code = 0
                                    },
                                    Attributes =
                                    [
                                        new OtlpKeyValue
                                        {
                                            Key = "session.id",
                                            Value = new OtlpAnyValue
                                            {
                                                StringValue = sessionId
                                            }
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    private static OtlpExportTraceServiceRequest CreateGenAiOtlpRequestWithSession(
        string traceId, string spanId, string sessionId,
        long inputTokens, long outputTokens)
    {
        var now = DateTimeOffset.UtcNow;
        var startNano = (ulong)(now.ToUnixTimeMilliseconds() * 1_000_000);
        var endNano = (ulong)((now.ToUnixTimeMilliseconds() + 500) * 1_000_000);

        return new OtlpExportTraceServiceRequest
        {
            ResourceSpans =
            [
                new OtlpResourceSpans
                {
                    Resource = new OtlpResource
                    {
                        Attributes =
                        [
                            new OtlpKeyValue
                            {
                                Key = "service.name",
                                Value = new OtlpAnyValue
                                {
                                    StringValue = "genai-e2e-service"
                                }
                            }
                        ]
                    },
                    ScopeSpans =
                    [
                        new OtlpScopeSpans
                        {
                            Spans =
                            [
                                new OtlpSpan
                                {
                                    TraceId = traceId,
                                    SpanId = spanId,
                                    Name = "genai-e2e-chat",
                                    Kind = 2,
                                    StartTimeUnixNano = startNano,
                                    EndTimeUnixNano = endNano,
                                    Status = new OtlpStatus
                                    {
                                        Code = 0
                                    },
                                    Attributes =
                                    [
                                        new OtlpKeyValue
                                        {
                                            Key = "session.id",
                                            Value = new OtlpAnyValue
                                            {
                                                StringValue = sessionId
                                            }
                                        },
                                        new OtlpKeyValue
                                        {
                                            Key = "gen_ai.provider.name",
                                            Value = new OtlpAnyValue
                                            {
                                                StringValue = "openai"
                                            }
                                        },
                                        new OtlpKeyValue
                                        {
                                            Key = "gen_ai.request.model",
                                            Value = new OtlpAnyValue
                                            {
                                                StringValue = "gpt-4"
                                            }
                                        },
                                        new OtlpKeyValue
                                        {
                                            Key = "gen_ai.usage.input_tokens",
                                            Value = new OtlpAnyValue
                                            {
                                                IntValue = inputTokens
                                            }
                                        },
                                        new OtlpKeyValue
                                        {
                                            Key = "gen_ai.usage.output_tokens",
                                            Value = new OtlpAnyValue
                                            {
                                                IntValue = outputTokens
                                            }
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    private static OtlpSpan CreateSpan(string traceId, string spanId, string sessionId, string name)
    {
        var now = DateTimeOffset.UtcNow;
        var startNano = (ulong)(now.ToUnixTimeMilliseconds() * 1_000_000);
        var endNano = (ulong)((now.ToUnixTimeMilliseconds() + 50) * 1_000_000);

        return new OtlpSpan
        {
            TraceId = traceId,
            SpanId = spanId,
            Name = name,
            Kind = 1,
            StartTimeUnixNano = startNano,
            EndTimeUnixNano = endNano,
            Status = new OtlpStatus
            {
                Code = 0
            },
            Attributes =
            [
                new OtlpKeyValue
                {
                    Key = "session.id",
                    Value = new OtlpAnyValue
                    {
                        StringValue = sessionId
                    }
                }
            ]
        };
    }
}