extern alias collector;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Api.Contracts.Common.Pagination;
using Xunit;

namespace Qyl.Collector.Contract.Tests;

/// <summary>
/// Proves the collector's served API is a live consumer of the generated qyl-api-schema
/// contract — not a hand-mapped lookalike — by seeding through the real OTLP ingest path
/// and validating the /api/v1 response deserializes strictly into the generated
/// <see cref="CursorPageTrace"/> DTO (UnmappedMemberHandling.Disallow rejects any field
/// that is not on the contract). Complements the static eng/build VerifyCollector* gates
/// with runtime serialization + DuckDB-projection fidelity.
/// </summary>
public sealed class CollectorTraceContractConformanceTests
{
    private const string SpanName = "contract-roundtrip-span";
    private const string ServiceName = "contract-test-svc";

    // Web defaults + the contract's [JsonPropertyName] snake_case; JsonStringEnumConverter
    // reads both string and integer enum encodings; Disallow rejects any non-contract field.
    private static readonly JsonSerializerOptions Strict = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task IngestedTrace_RoundTripsThroughApi_AsGeneratedContract()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var factory = new CollectorFactory();
        using var client = factory.CreateClient();

        // ---- seed via the real OTLP ingest endpoint (protobuf, as a real SDK would) ----
        var request = BuildSingleSpanTraceRequest();
        using var payload = new ByteArrayContent(request.ToByteArray());
        payload.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");

        var ingest = await client.PostAsync("/v1/traces", payload, ct);
        Assert.True(
            ingest.IsSuccessStatusCode,
            $"OTLP ingest failed: {(int)ingest.StatusCode} {await ingest.Content.ReadAsStringAsync(ct)}");

        // ---- read back through the public API and validate against the generated contract ----
        var page = await PollForTraceAsync(client, ct);

        Assert.NotNull(page);
        var trace = page.Items.Single(static t => t.Spans.Any(static s => s.Name == SpanName));
        Assert.Equal(1, trace.SpanCount);
        Assert.Contains(ServiceName, trace.Services);

        var span = trace.Spans.Single(static s => s.Name == SpanName);
        Assert.False(string.IsNullOrEmpty(span.TraceId));
        Assert.False(string.IsNullOrEmpty(span.SpanId));
    }

    // Ingestion may be buffered/batched, so poll the API until the projection surfaces the
    // span (or fail after the budget). Every response is strict-deserialized into the
    // contract DTO, so a shape mismatch fails fast regardless of timing.
    private static async Task<CursorPageTrace> PollForTraceAsync(HttpClient client, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var response = await client.GetAsync("/api/v1/traces", ct);
            Assert.True(
                response.IsSuccessStatusCode,
                $"GET /api/v1/traces failed: {(int)response.StatusCode}");

            var page = await response.Content.ReadFromJsonAsync<CursorPageTrace>(Strict, ct);
            if (page is not null && page.Items.Any(static t => t.Spans.Any(static s => s.Name == SpanName)))
                return page;

            await Task.Delay(TimeSpan.FromMilliseconds(250), TimeProvider.System, ct);
        }

        throw new Xunit.Sdk.XunitException(
            $"Span '{SpanName}' never surfaced through /api/v1/traces within the budget.");
    }

    // In-process collector over an in-memory DuckDB (no temp files, no shared state).
    private sealed class CollectorFactory : WebApplicationFactory<collector::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseSetting("QYL_DATA_PATH", ":memory:");
    }

    private static ExportTraceServiceRequest BuildSingleSpanTraceRequest()
    {
        var traceId = ByteString.CopyFrom(Enumerable.Range(1, 16).Select(static i => (byte)i).ToArray());
        var spanId = ByteString.CopyFrom(Enumerable.Range(1, 8).Select(static i => (byte)i).ToArray());

        return new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue
                            {
                                Key = "service.name",
                                Value = new AnyValue { StringValue = ServiceName },
                            },
                        },
                    },
                    ScopeSpans =
                    {
                        new ScopeSpans
                        {
                            Spans =
                            {
                                new Span
                                {
                                    TraceId = traceId,
                                    SpanId = spanId,
                                    Name = SpanName,
                                    Kind = Span.Types.SpanKind.Server,
                                    StartTimeUnixNano = 1_000_000_000UL,
                                    EndTimeUnixNano = 2_000_000_000UL,
                                    Status = new Status { Code = Status.Types.StatusCode.Ok },
                                },
                            },
                        },
                    },
                },
            },
        };
    }
}
