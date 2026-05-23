using System.Net;
using System.Text;
using qyl.mcp.Tools;

namespace Qyl.Mcp.Tests.Tools;

public sealed class AnomalyToolsTests
{
    [Fact]
    public async Task GetMetricBaselineAsync_UsesCollectorServiceNameQueryParameter()
    {
        using var client = CreateClient(static request =>
        {
            request.RequestUri?.PathAndQuery.Should().Contain("metric=gen_ai.client.token.usage");
            request.RequestUri?.PathAndQuery.Should().Contain("serviceName=orders-api");
            request.RequestUri?.PathAndQuery.Should().NotContain("service=orders-api");

            return JsonResponse(HttpStatusCode.OK, """
                                                   {
                                                     "metric": "gen_ai.client.token.usage",
                                                     "hours": 24,
                                                     "mean": 50,
                                                     "std_dev": 0,
                                                     "p50": 50,
                                                     "p95": 50,
                                                     "p99": 50,
                                                     "sample_count": 1
                                                   }
                                                   """);
        });
        var tool = new AnomalyTools(client);

        var output = await tool.GetMetricBaselineAsync(
            "gen_ai.client.token.usage",
            service: "orders-api",
            ct: TestContext.Current.CancellationToken);

        output.Should().Contain("# Metric Baseline - gen_ai.client.token.usage");
        output.Should().Contain("Samples: 1");
    }

    [Fact]
    public async Task GetMetricBaselineAsync_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var sawCancelledRequestToken = false;
        using var client = CreateClient((_, cancellationToken) =>
        {
            cancellationToken.IsCancellationRequested.Should().BeTrue();
            sawCancelledRequestToken = true;

            return JsonResponse(HttpStatusCode.OK, """
                                                   {
                                                     "metric": "request_count",
                                                     "hours": 24,
                                                     "mean": 1,
                                                     "std_dev": 0,
                                                     "p50": 1,
                                                     "p95": 1,
                                                     "p99": 1,
                                                     "sample_count": 1
                                                   }
                                                   """);
        });
        var tool = new AnomalyTools(client);

        var output = await tool.GetMetricBaselineAsync(
            "request_count",
            ct: cts.Token);

        sawCancelledRequestToken.Should().BeTrue();
        output.Should().Be("**Cancelled:** The operation was cancelled.");
    }

    [Fact]
    public async Task GetMetricBaselineAsync_ReturnsCollectorValidationMessage()
    {
        using var client = CreateClient(static _ => JsonResponse(
            HttpStatusCode.BadRequest,
            """{ "error": "Unknown metric 'missing_metric'. Valid metrics: request_count" }"""));
        var tool = new AnomalyTools(client);

        var output = await tool.GetMetricBaselineAsync(
            "missing_metric",
            ct: TestContext.Current.CancellationToken);

        output.Should().Be(
            "Metric baseline query rejected: Unknown metric 'missing_metric'. Valid metrics: request_count");
    }

    [Fact]
    public async Task DetectAnomaliesAsync_ReturnsCollectorValidationMessage()
    {
        using var client = CreateClient(static _ => JsonResponse(
            HttpStatusCode.BadRequest,
            """{ "error": "Query parameter 'sensitivity' must be greater than zero." }"""));
        var tool = new AnomalyTools(client);

        var output = await tool.DetectAnomaliesAsync(
            "request_count",
            sensitivity: 0,
            ct: TestContext.Current.CancellationToken);

        output.Should().Be(
            "Anomaly detection query rejected: Query parameter 'sensitivity' must be greater than zero.");
    }

    [Fact]
    public async Task ComparePeriodsAsync_ReturnsCollectorValidationMessage()
    {
        using var client = CreateClient(static _ => JsonResponse(
            HttpStatusCode.BadRequest,
            """{ "error": "period1Start must be earlier than period1End." }"""));
        var tool = new AnomalyTools(client);

        var output = await tool.ComparePeriodsAsync(
            "request_count",
            "2026-05-23T10:00:00Z",
            "2026-05-23T09:00:00Z",
            "2026-05-22T10:00:00Z",
            "2026-05-22T11:00:00Z",
            ct: TestContext.Current.CancellationToken);

        output.Should().Be(
            "Period comparison query rejected: period1Start must be earlier than period1End.");
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> send)
    {
        return CreateClient((request, _) => send(request));
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send)
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

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(send(request, cancellationToken));
        }
    }
}
