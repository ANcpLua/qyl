using System.Net;
using System.Text;
using System.Text.Json;
using qyl.mcp.Tools.Metrics;

namespace Qyl.Mcp.Tests.Tools;

public sealed class MetricsToolsTests
{
    [Fact]
    public async Task ListMetrics_FormatsSuccessfulCatalog()
    {
        using var client = CreateClient(static request =>
        {
            request.RequestUri?.PathAndQuery.Should().Be("/api/v1/metrics");

            return JsonResponse(HttpStatusCode.OK, """
                                                   {
                                                     "items": [
                                                       {
                                                         "name": "request_count",
                                                         "type": "sum",
                                                         "unit": "{span}",
                                                         "label_keys": [ "service.name" ],
                                                         "services": [ "orders-api" ],
                                                         "services_truncated": false,
                                                         "service_limit": 100,
                                                         "description": "Count of stored spans per time bucket."
                                                       }
                                                     ],
                                                     "next_cursor": null,
                                                     "prev_cursor": null,
                                                     "has_more": false
                                                   }
                                                   """);
        });
        var tool = new ListMetricsTool(client);

        var output = await tool.ListMetrics(ct: TestContext.Current.CancellationToken);

        output.Should().Contain("# Available Metrics (1)");
        output.Should().Contain("**Has more:** no");
        output.Should().Contain("| `request_count` | sum | {span} | `service.name` | `orders-api` | Count of stored spans per time bucket. |");
    }

    [Fact]
    public async Task ListMetrics_WithFilters_UsesPublicMetricPageContract()
    {
        using var client = CreateClient(static request =>
        {
            request.RequestUri?.PathAndQuery.Should().Be(
                "/api/v1/metrics?serviceName=orders-api&namePattern=token&limit=5&serviceLimit=1&cursor=10");

            return JsonResponse(HttpStatusCode.OK, """
                                                   {
                                                     "items": [
                                                       {
                                                         "name": "gen_ai.client.token.usage",
                                                         "type": "histogram",
                                                         "unit": "{token}",
                                                         "label_keys": [ "service.name", "gen_ai.token.type" ],
                                                         "services": [ "orders-api" ],
                                                         "services_truncated": true,
                                                         "service_limit": 1,
                                                         "description": "Number of input and output tokens used."
                                                       }
                                                     ],
                                                     "next_cursor": "15",
                                                     "prev_cursor": "5",
                                                     "has_more": true
                                                   }
                                                   """);
        });
        var tool = new ListMetricsTool(client);

        var output = await tool.ListMetrics(
            serviceName: "orders-api",
            namePattern: "token",
            limit: 5,
            serviceLimit: 1,
            cursor: "10",
            ct: TestContext.Current.CancellationToken);

        output.Should().Contain("# Available Metrics (1)");
        output.Should().Contain("**Has more:** yes");
        output.Should().Contain("**Next cursor:** `15`");
        output.Should().Contain("**Previous cursor:** `5`");
        output.Should().Contain("`orders-api` ... truncated at 1");
    }

    [Fact]
    public async Task ListMetrics_ReturnsCollectorValidationMessage()
    {
        using var client = CreateClient(static _ => JsonResponse(
            HttpStatusCode.BadRequest,
            """{ "error": "Project-scoped metrics are not available yet." }"""));
        var tool = new ListMetricsTool(client);

        var output = await tool.ListMetrics(ct: TestContext.Current.CancellationToken);

        output.Should().Be("List metrics rejected: Project-scoped metrics are not available yet.");
    }

    [Fact]
    public async Task QueryMetrics_FormatsSuccessfulSeries()
    {
        using var client = CreateClient(static async (request, ct) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri?.PathAndQuery.Should().Be("/api/v1/metrics/query");

            if (request.Content is null)
                return JsonResponse(HttpStatusCode.BadRequest, """{ "error": "missing body" }""");

            var json = await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            root.GetProperty("metric_name").GetString().Should().Be("gen_ai.client.token.usage");
            root.GetProperty("filters").GetProperty("service.name").GetString().Should().Be("orders-api");
            root.GetProperty("filters").GetProperty("gen_ai.token.type").GetString().Should().Be("input");
            root.GetProperty("start_time").GetString().Should().Be("2026-05-23T10:00:00Z");
            root.GetProperty("end_time").GetString().Should().Be("2026-05-23T11:00:00Z");
            root.GetProperty("step").GetString().Should().Be("1h");

            return JsonResponse(HttpStatusCode.OK, """
                                                   {
                                                     "metric_name": "gen_ai.client.token.usage",
                                                     "series": [
                                                       {
                                                         "labels": {
                                                           "service.name": "orders-api",
                                                           "gen_ai.token.type": "input"
                                                         },
                                                         "points": [
                                                           { "timestamp": "2026-05-23T10:00:00.0000000Z", "value": 30 }
                                                         ]
                                                       }
                                                     ]
                                                   }
                                                   """);
        });
        var tool = new QueryMetricsTool(client);

        var output = await tool.QueryMetrics(
            "gen_ai.client.token.usage",
            filter: "service.name=orders-api",
            from: "2026-05-23T10:00:00Z",
            to: "2026-05-23T11:00:00Z",
            interval: "1h",
            tokenType: "input",
            ct: TestContext.Current.CancellationToken);

        output.Should().Contain("# Metric: `gen_ai.client.token.usage`");
        output.Should().Contain("**Series:** 1");
        output.Should().Contain("## Series: `service.name=orders-api`, `gen_ai.token.type=input`");
        output.Should().Contain("| 2026-05-23T10:00:00.0000000Z | 30 |");
    }

    [Fact]
    public async Task QueryMetrics_WithGroupBy_UsesPublicMetricQueryContract()
    {
        using var client = CreateClient(static async (request, ct) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri?.PathAndQuery.Should().Be("/api/v1/metrics/query");

            if (request.Content is null)
                return JsonResponse(HttpStatusCode.BadRequest, """{ "error": "missing body" }""");

            var json = await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            root.GetProperty("metric_name").GetString().Should().Be("gen_ai.client.token.usage");
            root.GetProperty("filters").GetProperty("service.name").GetString().Should().Be("orders-api");
            root.GetProperty("filters").GetProperty("gen_ai.token.type").GetString().Should().Be("input");
            root.GetProperty("start_time").GetString().Should().Be("2026-05-23T10:00:00Z");
            root.GetProperty("end_time").GetString().Should().Be("2026-05-23T11:00:00Z");
            root.GetProperty("step").GetString().Should().Be("1h");

            var groupBy = root.GetProperty("group_by").EnumerateArray();
            groupBy.MoveNext().Should().BeTrue();
            groupBy.Current.GetString().Should().Be("service.name");
            groupBy.MoveNext().Should().BeTrue();
            groupBy.Current.GetString().Should().Be("gen_ai.token.type");
            groupBy.MoveNext().Should().BeFalse();

            return JsonResponse(HttpStatusCode.OK, """
                                                   {
                                                     "metric_name": "gen_ai.client.token.usage",
                                                     "series": [
                                                       {
                                                         "labels": {
                                                           "service.name": "orders-api",
                                                           "gen_ai.token.type": "input"
                                                         },
                                                         "points": [
                                                           { "timestamp": "2026-05-23T10:00:00.0000000Z", "value": 30 }
                                                         ]
                                                       }
                                                     ]
                                                   }
                                                   """);
        });
        var tool = new QueryMetricsTool(client);

        var output = await tool.QueryMetrics(
            "gen_ai.client.token.usage",
            filter: "service.name=orders-api",
            from: "2026-05-23T10:00:00Z",
            to: "2026-05-23T11:00:00Z",
            interval: "1h",
            tokenType: "input",
            groupBy: "service.name, gen_ai.token.type",
            ct: TestContext.Current.CancellationToken);

        output.Should().Contain("# Metric: `gen_ai.client.token.usage`");
        output.Should().Contain("**Series:** 1");
        output.Should().Contain("## Series: `service.name=orders-api`, `gen_ai.token.type=input`");
        output.Should().Contain("| 2026-05-23T10:00:00.0000000Z | 30 |");
    }

    [Fact]
    public async Task QueryMetrics_WithProviderAndRequestModel_UsesPublicMetricQueryContract()
    {
        using var client = CreateClient(static async (request, ct) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri?.PathAndQuery.Should().Be("/api/v1/metrics/query");

            if (request.Content is null)
                return JsonResponse(HttpStatusCode.BadRequest, """{ "error": "missing body" }""");

            var json = await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            root.GetProperty("metric_name").GetString().Should().Be("gen_ai.client.cost");
            root.GetProperty("filters").GetProperty("service.name").GetString().Should().Be("orders-api");
            root.GetProperty("filters").GetProperty("gen_ai.provider.name").GetString().Should().Be("openai");
            root.GetProperty("filters").GetProperty("gen_ai.request.model").GetString().Should().Be("gpt-5.5");
            root.GetProperty("start_time").GetString().Should().Be("2026-05-23T10:00:00Z");
            root.GetProperty("end_time").GetString().Should().Be("2026-05-23T11:00:00Z");
            root.GetProperty("step").GetString().Should().Be("1h");

            return JsonResponse(HttpStatusCode.OK, """
                                                   {
                                                     "metric_name": "gen_ai.client.cost",
                                                     "series": [
                                                       {
                                                         "labels": {
                                                           "service.name": "orders-api",
                                                           "gen_ai.provider.name": "openai",
                                                           "gen_ai.request.model": "gpt-5.5"
                                                         },
                                                         "points": [
                                                           { "timestamp": "2026-05-23T10:00:00.0000000Z", "value": 0.0025 }
                                                         ]
                                                       }
                                                     ]
                                                   }
                                                   """);
        });
        var tool = new QueryMetricsTool(client);

        var output = await tool.QueryMetrics(
            "gen_ai.client.cost",
            filter: "service.name=orders-api",
            from: "2026-05-23T10:00:00Z",
            to: "2026-05-23T11:00:00Z",
            interval: "1h",
            providerName: "openai",
            requestModel: "gpt-5.5",
            ct: TestContext.Current.CancellationToken);

        output.Should().Contain("# Metric: `gen_ai.client.cost`");
        output.Should().Contain("**Series:** 1");
        output.Should().Contain("## Series: `service.name=orders-api`, `gen_ai.provider.name=openai`, `gen_ai.request.model=gpt-5.5`");
        output.Should().Contain("| 2026-05-23T10:00:00.0000000Z | 0.0025 |");
    }

    [Fact]
    public async Task QueryMetrics_WithSeriesLimit_UsesPublicMetricQueryContractAndReportsTruncation()
    {
        using var client = CreateClient(static async (request, ct) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri?.PathAndQuery.Should().Be("/api/v1/metrics/query");

            if (request.Content is null)
                return JsonResponse(HttpStatusCode.BadRequest, """{ "error": "missing body" }""");

            var json = await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            root.GetProperty("metric_name").GetString().Should().Be("request_count");
            root.GetProperty("series_limit").GetInt32().Should().Be(1);
            root.GetProperty("start_time").GetString().Should().Be("2026-05-23T10:00:00Z");
            root.GetProperty("end_time").GetString().Should().Be("2026-05-23T11:00:00Z");

            return JsonResponse(HttpStatusCode.OK, """
                                                   {
                                                     "metric_name": "request_count",
                                                     "series_truncated": true,
                                                     "series_limit": 1,
                                                     "series": [
                                                       {
                                                         "labels": { "service.name": "orders-api" },
                                                         "points": [
                                                           { "timestamp": "2026-05-23T10:00:00.0000000Z", "value": 7 }
                                                         ]
                                                       }
                                                     ]
                                                   }
                                                   """);
        });
        var tool = new QueryMetricsTool(client);

        var output = await tool.QueryMetrics(
            "request_count",
            from: "2026-05-23T10:00:00Z",
            to: "2026-05-23T11:00:00Z",
            seriesLimit: 1,
            ct: TestContext.Current.CancellationToken);

        output.Should().Contain("# Metric: `request_count`");
        output.Should().Contain("**Series:** 1");
        output.Should().Contain("**Series limit:** 1 (truncated)");
        output.Should().Contain("## Series: `service.name=orders-api`");
    }

    [Fact]
    public async Task QueryMetrics_WithPointLimit_UsesPublicMetricQueryContractAndReportsTruncation()
    {
        using var client = CreateClient(static async (request, ct) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri?.PathAndQuery.Should().Be("/api/v1/metrics/query");

            if (request.Content is null)
                return JsonResponse(HttpStatusCode.BadRequest, """{ "error": "missing body" }""");

            var json = await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            root.GetProperty("metric_name").GetString().Should().Be("request_count");
            root.GetProperty("point_limit").GetInt32().Should().Be(2);
            root.GetProperty("start_time").GetString().Should().Be("2026-05-23T10:00:00Z");
            root.GetProperty("end_time").GetString().Should().Be("2026-05-23T11:00:00Z");

            return JsonResponse(HttpStatusCode.OK, """
                                                   {
                                                     "metric_name": "request_count",
                                                     "points_truncated": true,
                                                     "point_limit": 2,
                                                     "series": [
                                                       {
                                                         "labels": { "service.name": "orders-api" },
                                                         "points": [
                                                           { "timestamp": "2026-05-23T10:00:00.0000000Z", "value": 7 },
                                                           { "timestamp": "2026-05-23T10:01:00.0000000Z", "value": 3 }
                                                         ]
                                                       }
                                                     ]
                                                   }
                                                   """);
        });
        var tool = new QueryMetricsTool(client);

        var output = await tool.QueryMetrics(
            "request_count",
            from: "2026-05-23T10:00:00Z",
            to: "2026-05-23T11:00:00Z",
            pointLimit: 2,
            ct: TestContext.Current.CancellationToken);

        output.Should().Contain("# Metric: `request_count`");
        output.Should().Contain("**Point limit:** 2 (truncated)");
        output.Should().Contain("| 2026-05-23T10:01:00.0000000Z | 3 |");
    }

    [Fact]
    public async Task QueryMetrics_WithGenAiLabelFilter_UsesPublicMetricQueryContract()
    {
        var now = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
        using var client = CreateClient(static async (request, ct) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri?.PathAndQuery.Should().Be("/api/v1/metrics/query");

            if (request.Content is null)
                return JsonResponse(HttpStatusCode.BadRequest, """{ "error": "missing body" }""");

            var json = await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            root.GetProperty("metric_name").GetString().Should().Be("gen_ai.client.cost");
            root.GetProperty("filters").GetProperty("gen_ai.provider.name").GetString().Should().Be("openai");
            root.GetProperty("start_time").GetString().Should().Be("2026-05-22T12:00:00.0000000+00:00");
            root.GetProperty("end_time").GetString().Should().Be("2026-05-23T12:00:00.0000000+00:00");

            return JsonResponse(HttpStatusCode.OK, """
                                                   {
                                                     "metric_name": "gen_ai.client.cost",
                                                     "series": [
                                                       {
                                                         "labels": { "gen_ai.provider.name": "openai" },
                                                         "points": [
                                                           { "timestamp": "2026-05-23T10:00:00.0000000Z", "value": 0.0025 }
                                                         ]
                                                       }
                                                     ]
                                                   }
                                                   """);
        });
        var tool = new QueryMetricsTool(client, new FixedTimeProvider(now));

        var output = await tool.QueryMetrics(
            "gen_ai.client.cost",
            filter: "gen_ai.provider.name=openai",
            ct: TestContext.Current.CancellationToken);

        output.Should().Contain("# Metric: `gen_ai.client.cost`");
        output.Should().Contain("## Series: `gen_ai.provider.name=openai`");
    }

    [Fact]
    public async Task QueryMetrics_RejectsProviderParameterThatDuplicatesFilterLabel()
    {
        using var client = CreateClient(static _ => throw new InvalidOperationException("collector should not be called"));
        var tool = new QueryMetricsTool(client);

        var output = await tool.QueryMetrics(
            "gen_ai.client.cost",
            filter: "gen_ai.provider.name=anthropic",
            providerName: "openai",
            ct: TestContext.Current.CancellationToken);

        output.Should().Be(
            "Metric query rejected: Query parameter 'providerName' duplicates filter label gen_ai.provider.name.");
    }

    [Fact]
    public async Task QueryMetrics_RejectsEmptyGroupByBeforeCallingCollector()
    {
        using var client = CreateClient(static _ => throw new InvalidOperationException("collector should not be called"));
        var tool = new QueryMetricsTool(client);

        var output = await tool.QueryMetrics(
            "request_count",
            groupBy: ",",
            ct: TestContext.Current.CancellationToken);

        output.Should().Be("Metric query rejected: Query parameter 'groupBy' must include at least one label.");
    }

    [Fact]
    public async Task QueryMetrics_ReturnsCollectorValidationMessage()
    {
        using var client = CreateClient(static _ => JsonResponse(
            HttpStatusCode.BadRequest,
            """{ "error": "Query parameter 'filter' supports service.name=<value> only." }"""));
        var tool = new QueryMetricsTool(client);

        var output = await tool.QueryMetrics(
            "request_count",
            filter: "project=demo",
            ct: TestContext.Current.CancellationToken);

        output.Should().Be("Metric query rejected: Query parameter 'filter' supports service.name=<value> only.");
    }

    [Fact]
    public async Task QueryMetrics_ReturnsUnknownMetricMessage()
    {
        using var client = CreateClient(static _ => JsonResponse(
            HttpStatusCode.NotFound,
            """{ "error": "Unknown metric 'missing_metric'." }"""));
        var tool = new QueryMetricsTool(client);

        var output = await tool.QueryMetrics(
            "missing_metric",
            ct: TestContext.Current.CancellationToken);

        output.Should().Be("Metric `missing_metric` was not found. Unknown metric 'missing_metric'.");
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> send)
    {
        return CreateClient((request, _) => Task.FromResult(send(request)));
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    {
        return new HttpClient(new StubHttpMessageHandler(send))
        {
            BaseAddress = new Uri("https://collector.test")
        };
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return send(request, cancellationToken);
        }
    }
}
