using System.Net;
using System.Text.Json;
using ANcpLua.Agents.Testing.Http;
using qyl.mcp.Tools.Metrics;

namespace Qyl.Mcp.Tests.Tools;

public sealed class MetricsToolsTests
{
    private const string SuccessMetadataJson = """
        {
          "items": [
            {
              "name": "gen_ai.client.token.usage",
              "type": "histogram",
              "description": "Token usage",
              "unit": "{token}",
              "label_keys": [ "gen_ai.token.type", "service.name" ],
              "services": [ "orders-api" ],
              "services_truncated": true,
              "service_limit": 1
            }
          ],
          "next_cursor": "cursor-2",
          "has_more": true
        }
        """;

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
        using var handler = new FakeHttpMessageHandler();
        handler.WithResponse("/api/v1/metrics", HttpStatusCode.OK, SuccessMetadataJson);
        using var client = handler.BuildHttpClient("https://collector.test");

        var output = await new ListMetricsTool(client).ListMetrics(ct: TestContext.Current.CancellationToken);

        handler.Requests.Should().ContainSingle().Which.Url.PathAndQuery.Should().Be("/api/v1/metrics");
        output.Should().Contain("# Available Metrics (1)");
        output.Should().Contain("**Has more:** yes");
        output.Should().Contain("**Next cursor:** `cursor-2`");
        output.Should().Contain("| `gen_ai.client.token.usage` | histogram | {token} | `gen_ai.token.type`, `service.name` | `orders-api` ... truncated at 1 | Token usage |");
    }

    [Theory]
    [InlineData("orders-api", null, null, null, null, "/api/v1/metrics?serviceName=orders-api")]
    [InlineData(null, "token", 5, 1, "10", "/api/v1/metrics?namePattern=token&limit=5&serviceLimit=1&cursor=10")]
    [InlineData("orders-api", "token", 5, 1, "10", "/api/v1/metrics?serviceName=orders-api&namePattern=token&limit=5&serviceLimit=1&cursor=10")]
    public async Task ListMetrics_ForwardsFiltersToQueryString(
        string? serviceName, string? namePattern, int? limit, int? serviceLimit, string? cursor, string expectedPathAndQuery)
    {
        using var handler = new FakeHttpMessageHandler();
        handler.WithResponse("/api/v1/metrics", HttpStatusCode.OK, """{ "items": [], "has_more": false }""");
        using var client = handler.BuildHttpClient("https://collector.test");

        await new ListMetricsTool(client).ListMetrics(
            serviceName: serviceName, namePattern: namePattern, limit: limit, serviceLimit: serviceLimit, cursor: cursor,
            ct: TestContext.Current.CancellationToken);

        handler.Requests.Single().Url.PathAndQuery.Should().Be(expectedPathAndQuery);
    }

    [Fact]
    public async Task QueryMetrics_POST_v1_metrics_query()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.WithResponse("/api/v1/metrics/query", HttpStatusCode.OK, SuccessSeriesJson);
        using var client = handler.BuildHttpClient("https://collector.test");

        var output = await new QueryMetricsTool(client).QueryMetrics(
            "gen_ai.client.token.usage",
            from: "2026-05-23T10:00:00Z", to: "2026-05-23T11:00:00Z",
            ct: TestContext.Current.CancellationToken);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Url.PathAndQuery.Should().Be("/api/v1/metrics/query");
        output.Should().Contain("# Metric: `gen_ai.client.token.usage`");
        output.Should().Contain("**Series:** 1");
        output.Should().Contain("## Series: `service.name=orders-api`");
        output.Should().Contain("| 2026-05-23T10:00:00.0000000Z | 30 |");
    }

    [Fact]
    public async Task QueryMetrics_SendsCanonicalPayloadShape()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.WithRequestValidator(static req => AssertJsonBody(req, static body =>
        {
            body.GetProperty("metric_name").GetString().Should().Be("gen_ai.client.token.usage");
            body.GetProperty("filters").GetProperty("service.name").GetString().Should().Be("orders-api");
            body.GetProperty("filters").GetProperty("gen_ai.token.type").GetString().Should().Be("input");
            body.GetProperty("start_time").GetString().Should().Be("2026-05-23T10:00:00Z");
            body.GetProperty("end_time").GetString().Should().Be("2026-05-23T11:00:00Z");
            body.GetProperty("step").GetString().Should().Be("1h");
        }));
        handler.WithResponse("/api/v1/metrics/query", HttpStatusCode.OK, SuccessSeriesJson);
        using var client = handler.BuildHttpClient("https://collector.test");

        await new QueryMetricsTool(client).QueryMetrics(
            "gen_ai.client.token.usage",
            filter: "service.name=orders-api",
            from: "2026-05-23T10:00:00Z", to: "2026-05-23T11:00:00Z",
            interval: "1h", tokenType: "input",
            ct: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task QueryMetrics_ForwardsGroupByLabels()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.WithRequestValidator(static req => AssertJsonBody(req, static body =>
        {
            var groupBy = body.GetProperty("group_by").EnumerateArray()
                .Select(static item => item.GetString());

            groupBy.Should().Equal("service.name", "gen_ai.token.type");
        }));
        handler.WithResponse("/api/v1/metrics/query", HttpStatusCode.OK, SuccessSeriesJson);
        using var client = handler.BuildHttpClient("https://collector.test");

        await new QueryMetricsTool(client).QueryMetrics(
            "gen_ai.client.token.usage",
            filter: "service.name=orders-api",
            from: "2026-05-23T10:00:00Z", to: "2026-05-23T11:00:00Z",
            tokenType: "input", groupBy: "service.name, gen_ai.token.type",
            ct: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task QueryMetrics_ForwardsProviderModelAndLimits_AndReportsTruncation()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.WithRequestValidator(static req => AssertJsonBody(req, static body =>
        {
            var filters = body.GetProperty("filters");
            filters.GetProperty("service.name").GetString().Should().Be("orders-api");
            filters.GetProperty("gen_ai.provider.name").GetString().Should().Be("openai");
            filters.GetProperty("gen_ai.request.model").GetString().Should().Be("gpt-4o-mini");
            body.GetProperty("series_limit").GetInt32().Should().Be(1);
            body.GetProperty("point_limit").GetInt32().Should().Be(2);
        }));
        handler.WithResponse("/api/v1/metrics/query", HttpStatusCode.OK, """
            {
              "metric_name": "gen_ai.client.cost",
              "series_truncated": true,
              "series_limit": 1,
              "points_truncated": true,
              "point_limit": 2,
              "series": [
                {
                  "labels": {
                    "service.name": "orders-api",
                    "gen_ai.provider.name": "openai",
                    "gen_ai.request.model": "gpt-4o-mini"
                  },
                  "points": [
                    { "timestamp": "2026-05-23T10:00:00.0000000Z", "value": 0.0025 },
                    { "timestamp": "2026-05-23T10:01:00.0000000Z", "value": 0.0030 }
                  ]
                }
              ]
            }
            """);
        using var client = handler.BuildHttpClient("https://collector.test");

        var output = await new QueryMetricsTool(client).QueryMetrics(
            "gen_ai.client.cost",
            filter: "service.name=orders-api",
            from: "2026-05-23T10:00:00Z", to: "2026-05-23T11:00:00Z",
            providerName: "openai", requestModel: "gpt-4o-mini",
            seriesLimit: 1, pointLimit: 2,
            ct: TestContext.Current.CancellationToken);

        output.Should().Contain("**Series limit:** 1 (truncated)");
        output.Should().Contain("**Point limit:** 2 (truncated)");
        output.Should().Contain("`gen_ai.provider.name=openai`");
        output.Should().Contain("`gen_ai.request.model=gpt-4o-mini`");
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
        using var handler = new FakeHttpMessageHandler();
        handler.WithResponse(
            "/api/v1/metrics", HttpStatusCode.BadRequest,
            """{ "error": "Project-scoped metrics are not available yet." }""");
        using var client = handler.BuildHttpClient("https://collector.test");

        var output = await new ListMetricsTool(client).ListMetrics(ct: TestContext.Current.CancellationToken);

        output.Should().Be("List metrics rejected: Project-scoped metrics are not available yet.");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{ "error": "Query parameter 'filter' supports service.name=<value> only." }""", "Metric query rejected: Query parameter 'filter' supports service.name=<value> only.")]
    [InlineData(HttpStatusCode.NotFound, """{ "error": "Unknown metric 'request_count'." }""", "Metric `request_count` was not found. Unknown metric 'request_count'.")]
    public async Task QueryMetrics_FormatsCollectorError(HttpStatusCode status, string body, string expected)
    {
        using var handler = new FakeHttpMessageHandler();
        handler.WithResponse("/api/v1/metrics/query", status, body);
        using var client = handler.BuildHttpClient("https://collector.test");

        var output = await new QueryMetricsTool(client).QueryMetrics(
            "request_count", from: "2026-05-23T10:00:00Z", to: "2026-05-23T11:00:00Z",
            ct: TestContext.Current.CancellationToken);

        output.Should().Be(expected);
    }

    private static void AssertJsonBody(HttpRequestMessage request, Action<JsonElement> assert)
    {
        if (request.Content is null)
            throw new InvalidOperationException("Expected request body.");

        using var reader = new StreamReader(request.Content.ReadAsStream());
        using var document = JsonDocument.Parse(reader.ReadToEnd());
        assert(document.RootElement);
    }
}
