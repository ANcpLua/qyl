using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DuckDB.NET.Data;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests.Functional;

[Trait("Category", "Functional")]
[Collection(FunctionalCollection.Name)]
public sealed class AnomalyEndpointsTests
    : IClassFixture<AnomalyEndpointsTests.CollectorFactory>
{
    private readonly CollectorFactory _factory;

    public AnomalyEndpointsTests(CollectorFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_baseline_accepts_canonical_derived_token_metric()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var serviceName = $"anomaly-canonical-{suffix}";
        await SeedSpanAsync($"anomaly-canonical-1-{suffix}", serviceName, TokenStart, 250, 10, 5, ct);
        await SeedSpanAsync($"anomaly-canonical-2-{suffix}", serviceName, TokenStart.AddMinutes(5), 500, 20, 15, ct);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            $"/api/v1/analytics/anomaly/baseline?metric=gen_ai.client.token.usage&hours=24&serviceName={Uri.EscapeDataString(serviceName)}",
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        body.GetProperty("metric").GetString().Should().Be("gen_ai.client.token.usage");
        body.GetProperty("sampleCount").GetInt64().Should().Be(1);
        body.GetProperty("mean").GetDouble().Should().Be(50);
    }

    [Fact]
    public async Task Get_baseline_applies_canonical_derived_metric_predicate()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var serviceName = $"anomaly-predicate-{suffix}";
        await SeedSpanAsync($"anomaly-predicate-genai-{suffix}", serviceName, TokenStart, 250, 10, 5, ct);
        await SeedPlainSpanAsync($"anomaly-predicate-http-{suffix}", serviceName, TokenStart.AddMinutes(5), 10_000, ct);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            $"/api/v1/analytics/anomaly/baseline?metric=gen_ai.client.operation.duration&hours=24&serviceName={Uri.EscapeDataString(serviceName)}",
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        body.GetProperty("metric").GetString().Should().Be("gen_ai.client.operation.duration");
        body.GetProperty("sampleCount").GetInt64().Should().Be(1);
        body.GetProperty("mean").GetDouble().Should().Be(0.25d);
    }

    [Fact]
    public async Task Get_baseline_applies_service_name_filter()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var selectedService = $"anomaly-selected-{suffix}";
        var otherService = $"anomaly-other-{suffix}";
        await SeedSpanAsync($"anomaly-selected-{suffix}", selectedService, TokenStart, 250, 10, 5, ct);
        await SeedSpanAsync($"anomaly-other-{suffix}", otherService, TokenStart, 250, 900, 100, ct);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            $"/api/v1/analytics/anomaly/baseline?metric=gen_ai.client.token.usage&hours=24&serviceName={Uri.EscapeDataString(selectedService)}",
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);
        body.GetProperty("mean").GetDouble().Should().Be(15);
    }

    [Fact]
    public async Task Get_baseline_unknown_metric_lists_canonical_metric_names()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/v1/analytics/anomaly/baseline?metric=missing_metric",
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var errors = body.GetProperty("errors").GetProperty("metric").EnumerateArray()
            .Select(static item => item.GetString())
            .Where(static item => item is not null)
            .Select(static item => item ?? string.Empty);
        errors.Should().Contain(static message => message.Contains("gen_ai.client.token.usage", StringComparison.Ordinal));
    }

    // Relative anchor (top-of-hour, 2h back) so both seeded spans share one hourly bucket and stay inside the endpoint's hours=24 window; a fixed date ages out.
    private static DateTimeOffset TokenStart
    {
        get
        {
            var now = TimeProvider.System.GetUtcNow();
            var topOfHour = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
            return topOfHour.AddHours(-2);
        }
    }

    private async Task SeedSpanAsync(
        string spanId,
        string serviceName,
        DateTimeOffset start,
        int durationMs,
        long inputTokens,
        long outputTokens,
        CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<DuckDbStore>();
        var startNano = ToUnixNanoseconds(start);
        var durationNano = durationMs * 1_000_000L;

        await store.ExecuteWriteAsync(async (connection, token) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO spans
                                  (span_id, trace_id, name, kind, start_time_unix_nano, end_time_unix_nano,
                                   duration_ns, status_code, service_name, gen_ai_provider_name,
                                   gen_ai_request_model, gen_ai_input_tokens, gen_ai_output_tokens, gen_ai_cost_usd)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = spanId });
            cmd.Parameters.Add(new DuckDBParameter { Value = $"trace-{spanId}" });
            cmd.Parameters.Add(new DuckDBParameter { Value = "chat completion" });
            cmd.Parameters.Add(new DuckDBParameter { Value = "client" });
            cmd.Parameters.Add(new DuckDBParameter { Value = startNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = startNano + durationNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = durationNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = "1" });
            cmd.Parameters.Add(new DuckDBParameter { Value = serviceName });
            cmd.Parameters.Add(new DuckDBParameter { Value = "openai" });
            cmd.Parameters.Add(new DuckDBParameter { Value = "gpt-5.5" });
            cmd.Parameters.Add(new DuckDBParameter { Value = inputTokens });
            cmd.Parameters.Add(new DuckDBParameter { Value = outputTokens });
            cmd.Parameters.Add(new DuckDBParameter { Value = 0.0025d });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    private async Task SeedPlainSpanAsync(
        string spanId,
        string serviceName,
        DateTimeOffset start,
        int durationMs,
        CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<DuckDbStore>();
        var startNano = ToUnixNanoseconds(start);
        var durationNano = durationMs * 1_000_000L;

        await store.ExecuteWriteAsync(async (connection, token) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO spans
                                  (span_id, trace_id, name, kind, start_time_unix_nano, end_time_unix_nano,
                                   duration_ns, status_code, service_name, gen_ai_provider_name,
                                   gen_ai_request_model, gen_ai_input_tokens, gen_ai_output_tokens, gen_ai_cost_usd)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = spanId });
            cmd.Parameters.Add(new DuckDBParameter { Value = $"trace-{spanId}" });
            cmd.Parameters.Add(new DuckDBParameter { Value = "GET /health" });
            cmd.Parameters.Add(new DuckDBParameter { Value = "server" });
            cmd.Parameters.Add(new DuckDBParameter { Value = startNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = startNano + durationNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = durationNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = "1" });
            cmd.Parameters.Add(new DuckDBParameter { Value = serviceName });
            cmd.Parameters.Add(new DuckDBParameter { Value = DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = DBNull.Value });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    private static long ToUnixNanoseconds(DateTimeOffset value)
    {
        var seconds = value.ToUnixTimeSeconds();
        var ticksWithinSecond = value.Ticks % TimeSpan.TicksPerSecond;
        return (seconds * 1_000_000_000L) + (ticksWithinSecond * 100L);
    }

    public sealed class CollectorFactory() : CollectorFunctionalFactory("anomaly-metrics")
    {
    }
}
