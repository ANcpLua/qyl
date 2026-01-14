using System.Net;
using System.Text;
using System.Text.Json;
using qyl.collector.Ingestion;
using qyl.collector.Storage;

namespace qyl.collector.tests.Integration;

/// <summary>
///     Integration tests for OTLP JSON ingestion endpoints.
///     Tests POST /v1/traces (OpenTelemetry standard) and POST /api/v1/ingest (native).
/// </summary>
public sealed class OtlpIngestionTests : IClassFixture<QylWebApplicationFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions s_snakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly QylWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public OtlpIngestionTests(QylWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {QylWebApplicationFactory.TestToken}");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    // =========================================================================
    // OTLP /v1/traces Endpoint
    // =========================================================================

    [Fact]
    public async Task PostTraces_ValidOtlpJson_ReturnsAccepted()
    {
        var otlpRequest = CreateValidOtlpRequest();
        var json = JsonSerializer.Serialize(otlpRequest, QylSerializerContext.Default.OtlpExportTraceServiceRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/v1/traces", content);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task PostTraces_EmptyResourceSpans_ReturnsAccepted()
    {
        var otlpRequest = new OtlpExportTraceServiceRequest { ResourceSpans = [] };
        var json = JsonSerializer.Serialize(otlpRequest, QylSerializerContext.Default.OtlpExportTraceServiceRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/v1/traces", content);

        // Empty but valid request should be accepted
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task PostTraces_NullResourceSpans_ReturnsBadRequest()
    {
        var json = "{}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/v1/traces", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTraces_InvalidJson_ReturnsBadRequest()
    {
        var content = new StringContent("{ invalid json }", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/v1/traces", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTraces_WithGenAiAttributes_ReturnsAccepted()
    {
        var otlpRequest = CreateGenAiOtlpRequest();
        var json = JsonSerializer.Serialize(otlpRequest, QylSerializerContext.Default.OtlpExportTraceServiceRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/v1/traces", content);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    // =========================================================================
    // Native /api/v1/ingest Endpoint
    // =========================================================================

    [Fact]
    public async Task PostIngest_ValidSpanBatch_ReturnsAccepted()
    {
        var span = SpanBuilder.Create(TestConstants.TraceDefault, TestConstants.SpanDefault)
            .WithSessionId(TestConstants.SessionDefault)
            .Build();

        var batch = new { Spans = new[] { MapSpanToDto(span) } };
        var json = JsonSerializer.Serialize(batch, s_snakeCaseOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/ingest", content);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task PostIngest_EmptyBatch_ReturnsBadRequest()
    {
        var json = """{"spans": []}""";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/ingest", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostIngest_InvalidJson_ReturnsBadRequest()
    {
        var content = new StringContent("not valid json", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/ingest", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static OtlpExportTraceServiceRequest CreateValidOtlpRequest()
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
                                Value = new OtlpAnyValue { StringValue = "test-service" }
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
                                    TraceId = "0123456789abcdef0123456789abcdef",
                                    SpanId = "0123456789abcdef",
                                    Name = "test-span",
                                    Kind = 1,
                                    StartTimeUnixNano = startNano,
                                    EndTimeUnixNano = endNano,
                                    Status = new OtlpStatus { Code = 0 },
                                    Attributes = []
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    private static OtlpExportTraceServiceRequest CreateGenAiOtlpRequest()
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
                                Value = new OtlpAnyValue { StringValue = "genai-service" }
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
                                    TraceId = "abcdef0123456789abcdef0123456789",
                                    SpanId = "abcdef01234567",
                                    Name = "chat_completion",
                                    Kind = 2,
                                    StartTimeUnixNano = startNano,
                                    EndTimeUnixNano = endNano,
                                    Status = new OtlpStatus { Code = 0 },
                                    Attributes =
                                    [
                                        new OtlpKeyValue
                                        {
                                            Key = "gen_ai.provider.name",
                                            Value = new OtlpAnyValue { StringValue = "openai" }
                                        },
                                        new OtlpKeyValue
                                        {
                                            Key = "gen_ai.request.model",
                                            Value = new OtlpAnyValue { StringValue = "gpt-4" }
                                        },
                                        new OtlpKeyValue
                                        {
                                            Key = "gen_ai.usage.input_tokens",
                                            Value = new OtlpAnyValue { IntValue = 100 }
                                        },
                                        new OtlpKeyValue
                                        {
                                            Key = "gen_ai.usage.output_tokens",
                                            Value = new OtlpAnyValue { IntValue = 50 }
                                        },
                                        new OtlpKeyValue
                                        {
                                            Key = "session.id",
                                            Value = new OtlpAnyValue { StringValue = "test-session-genai" }
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

    private static object MapSpanToDto(SpanStorageRow span)
    {
        return new
        {
            trace_id = span.TraceId,
            span_id = span.SpanId,
            parent_span_id = span.ParentSpanId,
            name = span.Name,
            kind = span.Kind,
            start_time_unix_nano = span.StartTimeUnixNano,
            end_time_unix_nano = span.EndTimeUnixNano,
            duration_ns = span.DurationNs,
            status_code = span.StatusCode,
            status_message = span.StatusMessage,
            service_name = span.ServiceName,
            session_id = span.SessionId,
            gen_ai_system = span.GenAiSystem,
            gen_ai_request_model = span.GenAiRequestModel,
            gen_ai_response_model = span.GenAiResponseModel,
            gen_ai_input_tokens = span.GenAiInputTokens,
            gen_ai_output_tokens = span.GenAiOutputTokens,
            gen_ai_temperature = span.GenAiTemperature,
            gen_ai_stop_reason = span.GenAiStopReason,
            gen_ai_tool_name = span.GenAiToolName,
            gen_ai_tool_call_id = span.GenAiToolCallId,
            gen_ai_cost_usd = span.GenAiCostUsd,
            attributes_json = span.AttributesJson,
            resource_json = span.ResourceJson
        };
    }
}