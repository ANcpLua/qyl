extern alias collector;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Qyl.Api.Contracts.Common.Pagination;
using Xunit;

namespace Qyl.Collector.Contract.Tests;

/// <summary>
/// Proves the collector's served API is a live consumer of the generated qyl-api-schema
/// contract across both core OTLP telemetry shapes (traces and logs — the collector does
/// not ingest metrics). Each test seeds through the real OTLP ingest path, reads back
/// through /api/v1, and strict-deserializes the response into the generated contract page
/// (UnmappedMemberHandling.Disallow rejects any field not on the contract). This complements
/// the static eng/build VerifyCollector* fitness functions — which make hand-written lookalike
/// DTOs non-compilable — with runtime serialization + DuckDB-projection fidelity.
/// </summary>
public sealed class CollectorApiContractConformanceTests
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

        await IngestAsync(client, "/v1/traces", BuildSingleSpanTraceRequest().ToByteArray(), ct);

        var page = await PollAsync<CursorPageTrace>(
            client,
            "/api/v1/traces",
            static p => p.Items.Any(static t => t.Spans.Any(static s => s.Name == SpanName)),
            ct);

        var trace = page.Items.Single(static t => t.Spans.Any(static s => s.Name == SpanName));
        Assert.Equal(1, trace.SpanCount);
        Assert.Contains(ServiceName, trace.Services);

        var span = trace.Spans.Single(static s => s.Name == SpanName);
        Assert.False(string.IsNullOrEmpty(span.TraceId));
        Assert.False(string.IsNullOrEmpty(span.SpanId));
    }

    [Fact]
    public async Task IngestedLog_RoundTripsThroughApi_AsGeneratedContract()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new CollectorFactory();
        using var client = factory.CreateClient();

        await IngestAsync(client, "/v1/logs", BuildSingleLogRequest().ToByteArray(), ct);

        // Fresh in-memory DuckDB → the single ingested log is the only row; strict-deserializing
        // the page into the generated CursorPageLogRecord is the contract-conformance proof.
        var page = await PollAsync<CursorPageLogRecord>(
            client,
            "/api/v1/logs",
            static p => p.Items.Count > 0,
            ct);

        Assert.NotEmpty(page.Items);
    }

    private static async Task IngestAsync(HttpClient client, string path, byte[] body, CancellationToken ct)
    {
        using var payload = new ByteArrayContent(body);
        payload.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");

        var response = await client.PostAsync(path, payload, ct);
        Assert.True(
            response.IsSuccessStatusCode,
            $"OTLP ingest {path} failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}");
    }

    // Ingestion may be buffered/batched, so poll until the projection surfaces the item (or fail
    // after the budget). Every response is strict-deserialized into the generated contract page,
    // so a shape mismatch fails fast regardless of timing.
    private static async Task<TPage> PollAsync<TPage>(
        HttpClient client, string path, Func<TPage, bool> hasItem, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var response = await client.GetAsync(path, ct);
            Assert.True(response.IsSuccessStatusCode, $"GET {path} failed: {(int)response.StatusCode}");

            var page = await response.Content.ReadFromJsonAsync<TPage>(Strict, ct);
            if (page is not null && hasItem(page))
                return page;

            await Task.Delay(TimeSpan.FromMilliseconds(250), TimeProvider.System, ct);
        }

        throw new Xunit.Sdk.XunitException($"No matching item surfaced through {path} within the budget.");
    }

    // In-process collector over an in-memory DuckDB (no temp files, no shared state).
    private sealed class CollectorFactory : WebApplicationFactory<collector::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseSetting("QYL_DATA_PATH", ":memory:");
    }

    private static Resource ServiceResource() =>
        new()
        {
            Attributes =
            {
                new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = ServiceName } },
            },
        };

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
                    Resource = ServiceResource(),
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

    private static ExportLogsServiceRequest BuildSingleLogRequest()
    {
        return new ExportLogsServiceRequest
        {
            ResourceLogs =
            {
                new ResourceLogs
                {
                    Resource = ServiceResource(),
                    ScopeLogs =
                    {
                        new ScopeLogs
                        {
                            LogRecords =
                            {
                                new LogRecord
                                {
                                    TimeUnixNano = 1_500_000_000UL,
                                    ObservedTimeUnixNano = 1_500_000_000UL,
                                    SeverityNumber = SeverityNumber.Info,
                                    SeverityText = "INFO",
                                    Body = new AnyValue { StringValue = "contract-roundtrip-log" },
                                },
                            },
                        },
                    },
                },
            },
        };
    }
}
