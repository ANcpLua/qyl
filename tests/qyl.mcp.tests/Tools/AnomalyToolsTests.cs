using System.Net;
using ANcpLua.Agents.Testing.Http;
using qyl.mcp.Tools;

namespace Qyl.Mcp.Tests.Tools;

public sealed class AnomalyToolsTests
{
    private const string BaselineOk = """
        { "metric": "gen_ai.client.token.usage", "hours": 24, "mean": 1, "std_dev": 0,
          "p50": 1, "p95": 1, "p99": 1, "sample_count": 1 }
        """;

    [Fact]
    public async Task GetMetricBaselineAsync_UsesCollectorServiceNameQueryParameter()
    {
        using var handler = new FakeHttpMessageHandler();
        handler.WithResponse("/api/v1/analytics/anomaly/baseline", HttpStatusCode.OK, BaselineOk);
        using var client = handler.BuildHttpClient("https://collector.test");

        var output = await new AnomalyTools(client).GetMetricBaselineAsync(
            "gen_ai.client.token.usage",
            service: "orders-api",
            ct: TestContext.Current.CancellationToken);

        var url = handler.Requests.Single().Url.PathAndQuery;
        url.Should().Contain("metric=gen_ai.client.token.usage");
        url.Should().Contain("serviceName=orders-api");
        url.Should().NotContain("service=orders-api");
        output.Should().Contain("# Metric Baseline - gen_ai.client.token.usage");
        output.Should().Contain("Window: 24h, Samples: 1");
    }

    [Fact]
    public async Task GetMetricBaselineAsync_ReturnsCancelledMessage_WhenCancelledBeforeRequest()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var handler = new FakeHttpMessageHandler();
        handler.WithResponse("/api/v1/analytics/anomaly/baseline", HttpStatusCode.OK, BaselineOk);
        using var client = handler.BuildHttpClient("https://collector.test");

        var output = await new AnomalyTools(client).GetMetricBaselineAsync("request_count", ct: cts.Token);

        output.Should().Be("**Cancelled:** The operation was cancelled.");
    }

    public static TheoryData<Func<AnomalyTools, Task<string>>, string, string, string> RejectionCases() =>
        new()
        {
            {
                static tools => tools.GetMetricBaselineAsync("missing_metric", ct: TestContext.Current.CancellationToken),
                "/api/v1/analytics/anomaly/baseline",
                """{ "error": "Unknown metric 'missing_metric'. Valid metrics: request_count" }""",
                "Metric baseline query rejected: Unknown metric 'missing_metric'. Valid metrics: request_count"
            },
            {
                static tools => tools.DetectAnomaliesAsync("request_count", sensitivity: 0, ct: TestContext.Current.CancellationToken),
                "/api/v1/analytics/anomaly/anomalies",
                """{ "error": "Query parameter 'sensitivity' must be greater than zero." }""",
                "Anomaly detection query rejected: Query parameter 'sensitivity' must be greater than zero."
            },
            {
                static tools => tools.ComparePeriodsAsync(
                    "request_count",
                    "2026-05-23T10:00:00Z", "2026-05-23T09:00:00Z",
                    "2026-05-22T10:00:00Z", "2026-05-22T11:00:00Z",
                    ct: TestContext.Current.CancellationToken),
                "/api/v1/analytics/anomaly/compare",
                """{ "error": "period1Start must be earlier than period1End." }""",
                "Period comparison query rejected: period1Start must be earlier than period1End."
            },
        };

    [Theory]
    [MemberData(nameof(RejectionCases))]
    public async Task AnomalyTools_FormatsCollectorValidationMessage(
        Func<AnomalyTools, Task<string>> call, string endpoint, string collectorBody, string expected)
    {
        using var handler = new FakeHttpMessageHandler();
        handler.WithResponse(endpoint, HttpStatusCode.BadRequest, collectorBody);
        using var client = handler.BuildHttpClient("https://collector.test");

        var output = await call(new AnomalyTools(client));

        output.Should().Be(expected);
    }
}
