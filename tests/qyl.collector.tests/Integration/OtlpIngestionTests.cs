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
    private static readonly JsonSerializerOptions SCamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly QylWebApplicationFactory _factory;
    private HttpClient? _client;

    public OtlpIngestionTests(QylWebApplicationFactory factory) => _factory = factory;

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

    private HttpClient Client => _client ?? throw new InvalidOperationException("Client not initialized");

    // =========================================================================
    // OTLP /v1/traces Endpoint
    // =========================================================================

    [Fact]
    public async Task PostTraces_ValidOtlpJson_ReturnsAccepted()
    {
        var otlpRequest = CreateValidOtlpRequest();
        var json = JsonSerializer.Serialize(otlpRequest, QylSerializerContext.Default.OtlpExportTraceServiceRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/v1/traces", content);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task PostTraces_EmptyResourceSpans_ReturnsAccepted()
    {
        var otlpRequest = new OtlpExportTraceServiceRequest
        {
            ResourceSpans = []
        };
        var json = JsonSerializer.Serialize(otlpRequest, QylSerializerContext.Default.OtlpExportTraceServiceRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/v1/traces", content);

        // Empty but valid request should be accepted
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task PostTraces_NullResourceSpans_ReturnsBadRequest()
    {
        const string json = "{}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/v1/traces", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTraces_InvalidJson_ReturnsBadRequest()
    {
        var content = new StringContent("{ invalid json }", Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/v1/traces", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTraces_WithGenAiAttributes_ReturnsAccepted()
    {
        var otlpRequest = CreateGenAiOtlpRequest();
        var json = JsonSerializer.Serialize(otlpRequest, QylSerializerContext.Default.OtlpExportTraceServiceRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/v1/traces", content);

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

        var batch = new
        {
            Spans = new[]
            {
                MapSpanToDto(span)
            }
        };
        var json = JsonSerializer.Serialize(batch, SCamelCaseOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/v1/ingest", content);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task PostIngest_EmptyBatch_ReturnsBadRequest()
    {
        const string json = """{"spans": []}""";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/v1/ingest", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostIngest_InvalidJson_ReturnsBadRequest()
    {
        var content = new StringContent("not valid json", Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/v1/ingest", content);

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
                                Value = new OtlpAnyValue
                                {
                                    StringValue = "test-service"
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
                                    TraceId = "0123456789abcdef0123456789abcdef",
                                    SpanId = "0123456789abcdef",
                                    Name = "test-span",
                                    Kind = 1,
                                    StartTimeUnixNano = startNano,
                                    EndTimeUnixNano = endNano,
                                    Status = new OtlpStatus
                                    {
                                        Code = 0
                                    },
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
                                Value = new OtlpAnyValue
                                {
                                    StringValue = "genai-service"
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
                                    TraceId = "abcdef0123456789abcdef0123456789",
                                    SpanId = "abcdef01234567",
                                    Name = "chat_completion",
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
                                                IntValue = 100
                                            }
                                        },
                                        new OtlpKeyValue
                                        {
                                            Key = "gen_ai.usage.output_tokens",
                                            Value = new OtlpAnyValue
                                            {
                                                IntValue = 50
                                            }
                                        },
                                        new OtlpKeyValue
                                        {
                                            Key = "session.id",
                                            Value = new OtlpAnyValue
                                            {
                                                StringValue = "test-session-genai"
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

    private static object MapSpanToDto(SpanStorageRow span) =>
        new
        {
            TraceId = span.TraceId,
            SpanId = span.SpanId,
            ParentSpanId = span.ParentSpanId,
            Name = span.Name,
            Kind = span.Kind,
            StartTimeUnixNano = span.StartTimeUnixNano,
            EndTimeUnixNano = span.EndTimeUnixNano,
            DurationNs = span.DurationNs,
            StatusCode = span.StatusCode,
            StatusMessage = span.StatusMessage,
            ServiceName = span.ServiceName,
            SessionId = span.SessionId,
            GenAiProviderName = span.GenAiProviderName,
            GenAiRequestModel = span.GenAiRequestModel,
            GenAiResponseModel = span.GenAiResponseModel,
            GenAiInputTokens = span.GenAiInputTokens,
            GenAiOutputTokens = span.GenAiOutputTokens,
            GenAiTemperature = span.GenAiTemperature,
            GenAiStopReason = span.GenAiStopReason,
            GenAiToolName = span.GenAiToolName,
            GenAiToolCallId = span.GenAiToolCallId,
            GenAiCostUsd = span.GenAiCostUsd,
            AttributesJson = span.AttributesJson,
            ResourceJson = span.ResourceJson
        };
}
