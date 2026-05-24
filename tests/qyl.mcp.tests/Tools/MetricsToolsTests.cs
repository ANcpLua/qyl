using System.Net;
using System.Text.Json;
using ANcpLua.Agents.Testing.Http;
using qyl.mcp.Tools.Metrics;

namespace Qyl.Mcp.Tests.Tools;

public sealed class MetricsToolsTests
{
    private const string SuccessSeriesJson = """
        {
          "metric_name": "gen_ai.client.token.usage",
          "series": [
            {
              "labels": { "service.name": "orders-api" },
              "points": [ { "timestamp": "2026-05-23T10:00:00.0000000Z", "value": 30 } ]
            }
          ]
        }
        """;

    [Fact]
    public async Task ListMetrics_GET_v1_metrics()
    {
        using var handler = new FakeHttpMessageHandler()
            .WithResponse("/api/v1/metrics", HttpStatusCode.OK, """{ "items": [], "has_more": false }""");
        using var client = handler.BuildHttpClient("https://collector.test");

        await new ListMetricsTool(client).ListMetrics(ct: TestContext.Current.CancellationToken);

        handler.Requests.Should().ContainSingle().Which.Url.PathAndQuery.Should().Be("/api/v1/metrics");
    }

    [Theory]
    [InlineData("orders-api", null, null, null, null, "/api/v1/metrics?serviceName=orders-api")]
    [InlineData(null, "token", 5, 1, "10", "/api/v1/metrics?namePattern=token&limit=5&serviceLimit=1&cursor=10")]
    [InlineData("orders-api", "token", 5, 1, "10", "/api/v1/metrics?serviceName=orders-api&namePattern=token&limit=5&serviceLimit=1&cursor=10")]
    public async Task ListMetrics_ForwardsFiltersToQueryString(
        string? serviceName, string? namePattern, int? limit, int? serviceLimit, string? cursor, string expectedPathAndQuery)
    {
        using var handler = new FakeHttpMessageHandler()
            .WithResponse("/api/v1/metrics", HttpStatusCode.OK, """{ "items": [], "has_more": false }""");
        using var client = handler.BuildHttpClient("https://collector.test");

        await new ListMetricsTool(client).ListMetrics(
            serviceName: serviceName, namePattern: namePattern, limit: limit, serviceLimit: serviceLimit, cursor: cursor,
            ct: TestContext.Current.CancellationToken);

        handler.Requests.Single().Url.PathAndQuery.Should().Be(expectedPathAndQuery);
    }

    [Fact]
    public async Task QueryMetrics_POST_v1_metrics_query()
    {
        using var handler = new FakeHttpMessageHandler()
            .WithResponse("/api/v1/metrics/query", HttpStatusCode.OK, SuccessSeriesJson);
        using var client = handler.BuildHttpClient("https://collector.test");

        await new QueryMetricsTool(client).QueryMetrics(
            "gen_ai.client.token.usage",
            from: "2026-05-23T10:00:00Z", to: "2026-05-23T11:00:00Z",
            ct: TestContext.Current.CancellationToken);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Url.PathAndQuery.Should().Be("/api/v1/metrics/query");
    }

    [Fact]
    public async Task QueryMetrics_SendsCanonicalPayloadShape()
    {
        string? rawBody = null;
        using var handler = new FakeHttpMessageHandler()
            .WithRequestValidator(req =>
            {
                if (req.Content is null) return;
                using var reader = new StreamReader(req.Content.ReadAsStream());
                rawBody = reader.ReadToEnd();
            })
            .WithResponse("/api/v1/metrics/query", HttpStatusCode.OK, SuccessSeriesJson);
        using var client = handler.BuildHttpClient("https://collector.test");

        await new QueryMetricsTool(client).QueryMetrics(
            "gen_ai.client.token.usage",
            filter: "service.name=orders-api",
            from: "2026-05-23T10:00:00Z", to: "2026-05-23T11:00:00Z",
            interval: "1h", tokenType: "input",
            ct: TestContext.Current.CancellationToken);

        rawBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(rawBody!);
        var body = doc.RootElement;
        body.GetProperty("metric_name").GetString().Should().Be("gen_ai.client.token.usage");
        body.GetProperty("filters").GetProperty("service.name").GetString().Should().Be("orders-api");
        body.GetProperty("filters").GetProperty("gen_ai.token.type").GetString().Should().Be("input");
        body.GetProperty("start_time").GetString().Should().Be("2026-05-23T10:00:00Z");
        body.GetProperty("end_time").GetString().Should().Be("2026-05-23T11:00:00Z");
        body.GetProperty("step").GetString().Should().Be("1h");
    }

    [Fact]
    public async Task QueryMetrics_RejectsProviderDuplicatingFilterLabel_WithoutCallingCollector()
    {
        using var handler = new FakeHttpMessageHandler();
        using var client = handler.BuildHttpClient("https://collector.test");

        var output = await new QueryMetricsTool(client).QueryMetrics(
            "gen_ai.client.cost",
            filter: "gen_ai.provider.name=anthropic",
            providerName: "openai",
            ct: TestContext.Current.CancellationToken);

        output.Should().Contain("duplicates filter label gen_ai.provider.name");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryMetrics_RejectsEmptyGroupBy_WithoutCallingCollector()
    {
        using var handler = new FakeHttpMessageHandler();
        using var client = handler.BuildHttpClient("https://collector.test");

        var output = await new QueryMetricsTool(client).QueryMetrics(
            "request_count",
            groupBy: ",",
            ct: TestContext.Current.CancellationToken);

        output.Should().Contain("must include at least one label");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ListMetrics_FormatsCollector400_AsRejection()
    {
        using var handler = new FakeHttpMessageHandler().WithResponse(
            "/api/v1/metrics", HttpStatusCode.BadRequest,
            """{ "error": "Project-scoped metrics are not available yet." }""");
        using var client = handler.BuildHttpClient("https://collector.test");

        var output = await new ListMetricsTool(client).ListMetrics(ct: TestContext.Current.CancellationToken);

        output.Should().Be("List metrics rejected: Project-scoped metrics are not available yet.");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{ "error": "Query parameter 'filter' supports service.name=<value> only." }""", "Metric query rejected: Query parameter 'filter' supports service.name=<value> only.")]
    [InlineData(HttpStatusCode.NotFound, """{ "error": "Unknown metric 'missing_metric'." }""", "Metric `request_count` was not found. Unknown metric 'missing_metric'.")]
    public async Task QueryMetrics_FormatsCollectorError(HttpStatusCode status, string body, string expected)
    {
        using var handler = new FakeHttpMessageHandler().WithResponse("/api/v1/metrics/query", status, body);
        using var client = handler.BuildHttpClient("https://collector.test");

        var output = await new QueryMetricsTool(client).QueryMetrics(
            "request_count", from: "2026-05-23T10:00:00Z", to: "2026-05-23T11:00:00Z",
            ct: TestContext.Current.CancellationToken);

        output.Should().Be(expected);
    }
}
