using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Qyl.E2E.Tests.Topology;

namespace Qyl.E2E.Tests.Scenarios;

/// <summary>
/// First real E2E scenario: an OTLP/HTTP JSON trace POSTed to the running
/// collector container reaches DuckDB storage and is reflected in the
/// telemetry-stats endpoint. Exercises HTTP receiver -> JSON parse ->
/// OtlpConverter -> SpanRingBuffer.PushRange -> DuckDbStore.EnqueueAsync ->
/// stats aggregate query, all inside a real container.
///
/// The richer roundtrip target (`GET /api/v1/traces`) is intentionally avoided
/// because it currently returns HTTP 500 due to a schema/type mismatch
/// (spans.kind + spans.status_code are VARCHAR in DuckDbSchema.g.sql but
/// SpanStorageRow.Kind/StatusCode are `byte`, so the generated MapFromReader
/// throws `InvalidCastException: System.String -> System.Byte`). That is a
/// pre-existing production bug to fix in a follow-up; this scenario already
/// proves the ingest pipeline end-to-end via the working stats endpoint.
/// </summary>
[Trait("Category", "E2E")]
[Collection(E2ECollection.Name)]
public sealed class OtlpHttpTraceIngestionRoundtripTests(QylTopologyFixture topology)
{
    private static readonly TimeSpan s_pollTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromMilliseconds(250);

    [Fact]
    public async Task OtlpJsonSpanIsAcceptedAndIncrementsTelemetryStats()
    {
        var ct = TestContext.Current.CancellationToken;

        using var http = new HttpClient { BaseAddress = topology.CollectorBaseUrl };

        var initialStats = await ReadStatsAsync(http, ct);
        var initialSpanCount = initialStats.SpanCount;

        var traceId = Guid.NewGuid().ToString("N");
        var spanId = Guid.NewGuid().ToString("N")[..16];
        var serviceName = $"qyl-e2e-otlp-{Guid.NewGuid():N}";
        var spanName = $"e2e-span-{Guid.NewGuid():N}";

        var nowNanos = (ulong)TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds() * 1_000_000UL;
        var payload = new
        {
            resourceSpans = new[]
            {
                new
                {
                    resource = new
                    {
                        attributes = new[]
                        {
                            new { key = "service.name", value = new { stringValue = serviceName } },
                        },
                    },
                    scopeSpans = new[]
                    {
                        new
                        {
                            spans = new[]
                            {
                                new
                                {
                                    traceId,
                                    spanId,
                                    name = spanName,
                                    kind = 1,
                                    startTimeUnixNano = nowNanos,
                                    endTimeUnixNano = nowNanos + 1_000_000UL,
                                    status = new { code = 1 },
                                },
                            },
                        },
                    },
                },
            },
        };

        using var ingestResponse = await http.PostAsJsonAsync("v1/traces", payload, ct);
        ingestResponse.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "OTLP/HTTP JSON ingest must accept a well-formed payload");

        var finalStats = await PollUntilSpanCountIncreasesAsync(http, initialSpanCount, ct);

        finalStats.SpanCount.Should().BeGreaterThan(initialSpanCount,
            "the collector's storage stats must reflect the ingested span");
        finalStats.NewestSpanTime.Should().BeGreaterThanOrEqualTo(nowNanos,
            "the newest stored span timestamp must include our just-ingested span");
    }

    private static async Task<TelemetryStats> ReadStatsAsync(HttpClient http, CancellationToken ct)
    {
        using var response = await http.GetAsync("api/v1/telemetry/stats", ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var root = doc.RootElement;
        return new TelemetryStats(
            SpanCount: ReadInt64(root, "spanCount"),
            NewestSpanTime: ReadUInt64(root, "newestSpanTime"));
    }

    private static async Task<TelemetryStats> PollUntilSpanCountIncreasesAsync(
        HttpClient http,
        long baseline,
        CancellationToken ct)
    {
        var deadline = TimeProvider.System.GetUtcNow() + s_pollTimeout;
        TelemetryStats latest = default;

        while (TimeProvider.System.GetUtcNow() < deadline)
        {
            latest = await ReadStatsAsync(http, ct);
            if (latest.SpanCount > baseline) return latest;
            await Task.Delay(s_pollInterval, ct);
        }

        return latest;
    }

    private static long ReadInt64(JsonElement root, string property) =>
        root.TryGetProperty(property, out var v) && v.ValueKind is JsonValueKind.Number
            ? v.GetInt64()
            : 0L;

    private static ulong ReadUInt64(JsonElement root, string property) =>
        root.TryGetProperty(property, out var v) && v.ValueKind is JsonValueKind.Number
            ? v.GetUInt64()
            : 0UL;

    private readonly record struct TelemetryStats(long SpanCount, ulong NewestSpanTime);
}
