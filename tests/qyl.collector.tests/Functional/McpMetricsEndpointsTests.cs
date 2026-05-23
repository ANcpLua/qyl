using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DuckDB.NET.Data;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests.Functional;

[Trait("Category", "Functional")]
[Collection(FunctionalCollection.Name)]
public sealed class McpMetricsEndpointsTests
    : IClassFixture<McpMetricsEndpointsTests.CollectorFactory>
{
    private readonly CollectorFactory _factory;

    public McpMetricsEndpointsTests(CollectorFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_mcp_metrics_lists_collector_wide_derived_catalog()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/mcp/metrics", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        body.EnumerateArray()
            .Any(static metric =>
                metric.GetProperty("name").GetString() == "gen_ai.client.token.usage" &&
                metric.GetProperty("type").GetString() == "histogram" &&
                metric.GetProperty("unit").GetString() == "{token}" &&
                metric.GetProperty("label_keys").EnumerateArray()
                    .Select(static label => label.GetString())
                    .Contains("gen_ai.provider.name") &&
                metric.GetProperty("label_keys").EnumerateArray()
                    .Select(static label => label.GetString())
                    .Contains("gen_ai.request.model") &&
                metric.GetProperty("label_keys").EnumerateArray()
                    .Select(static label => label.GetString())
                    .Contains("gen_ai.token.type") &&
                (metric.GetProperty("description").GetString() ?? string.Empty)
                .Contains("derived", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Get_mcp_metrics_rejects_project_scope_until_spans_have_project_dimension()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/mcp/metrics?project=demo", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain("Project-scoped metrics");
    }

    [Fact]
    public async Task Get_mcp_metric_query_returns_time_series_from_stored_spans()
    {
        var ct = TestContext.Current.CancellationToken;
        var spanSuffix = Guid.NewGuid().ToString("N");
        var serviceName = $"orders-api-{spanSuffix}";
        var start = new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.Zero);
        await SeedSpanAsync(
            $"metric-span-1-{spanSuffix}",
            serviceName,
            start,
            durationMs: 250,
            inputTokens: 10,
            outputTokens: 5,
            ct);
        await SeedSpanAsync(
            $"metric-span-2-{spanSuffix}",
            serviceName,
            start.AddMinutes(5),
            durationMs: 500,
            inputTokens: 20,
            outputTokens: 15,
            ct);

        using var client = _factory.CreateClient();
        var from = Uri.EscapeDataString(start.AddMinutes(-1).ToString("O"));
        var to = Uri.EscapeDataString(start.AddHours(1).ToString("O"));
        var filter = Uri.EscapeDataString($"service.name={serviceName}");

        using var response = await client.GetAsync(
            $"/api/v1/mcp/metrics/gen_ai.client.token.usage/query?from={from}&to={to}&interval=1h&filter={filter}",
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);

        body.GetProperty("metric").GetString().Should().Be("gen_ai.client.token.usage");
        body.GetProperty("labels").GetProperty("service.name").GetString().Should().Be(serviceName);
        var point = body.GetProperty("points").EnumerateArray().Should().ContainSingle().Subject;
        point.GetProperty("value").GetDouble().Should().Be(50);
    }

    [Fact]
    public async Task Get_mcp_metric_query_filters_genai_token_usage_by_token_type()
    {
        var ct = TestContext.Current.CancellationToken;
        var spanSuffix = Guid.NewGuid().ToString("N");
        var serviceName = $"orders-api-token-filter-{spanSuffix}";
        var start = new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.Zero);
        await SeedSpanAsync(
            $"metric-token-filter-1-{spanSuffix}",
            serviceName,
            start,
            durationMs: 250,
            inputTokens: 10,
            outputTokens: 5,
            ct);
        await SeedSpanAsync(
            $"metric-token-filter-2-{spanSuffix}",
            serviceName,
            start.AddMinutes(5),
            durationMs: 500,
            inputTokens: 20,
            outputTokens: 15,
            ct);

        using var client = _factory.CreateClient();
        var from = Uri.EscapeDataString(start.AddMinutes(-1).ToString("O"));
        var to = Uri.EscapeDataString(start.AddHours(1).ToString("O"));
        var filter = Uri.EscapeDataString($"service.name={serviceName}");

        using var response = await client.GetAsync(
            $"/api/v1/mcp/metrics/gen_ai.client.token.usage/query?from={from}&to={to}&interval=1h&filter={filter}&tokenType=input",
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var body = JsonSerializer.Deserialize<JsonElement>(responseBody);

        body.GetProperty("metric").GetString().Should().Be("gen_ai.client.token.usage");
        body.GetProperty("labels").GetProperty("service.name").GetString().Should().Be(serviceName);
        body.GetProperty("labels").GetProperty("gen_ai.token.type").GetString().Should().Be("input");
        var point = body.GetProperty("points").EnumerateArray().Should().ContainSingle().Subject;
        point.GetProperty("value").GetDouble().Should().Be(30);
    }

    [Fact]
    public async Task Get_mcp_metric_query_rejects_token_type_for_non_genai_token_metric()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/v1/mcp/metrics/request_count/query?tokenType=input",
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain("tokenType");
    }

    [Fact]
    public async Task Get_mcp_metric_query_rejects_unsupported_filter_shape()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/v1/mcp/metrics/request_count/query?filter=project%3Ddemo",
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("error").GetString().Should().Contain("service.name");
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
            cmd.Parameters.Add(new DuckDBParameter { Value = "trace-metrics" });
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

    private static long ToUnixNanoseconds(DateTimeOffset value)
    {
        var seconds = value.ToUnixTimeSeconds();
        var ticksWithinSecond = value.Ticks % TimeSpan.TicksPerSecond;
        return (seconds * 1_000_000_000L) + (ticksWithinSecond * 100L);
    }

    public sealed class CollectorFactory() : CollectorFunctionalFactory("mcp-metrics")
    {
    }
}
