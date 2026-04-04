namespace Qyl.Mcp.Tests;

public sealed class McpCollectorHttpClientExtensionsTests
{
    [Fact]
    public void AddCollectorHttpClient_InvalidCollectorUrl_ThrowsInvalidOperationException()
    {
        ServiceCollection services = [];

        var act = () => services.AddCollectorHttpClient("collector-without-scheme");

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Be("Invalid QYL_COLLECTOR_URL 'collector-without-scheme'.");
    }

    [Fact]
    public void AddCollectorHttpClient_NonPositiveTimeout_ThrowsArgumentOutOfRangeException()
    {
        ServiceCollection services = [];

        var act = () => services.AddCollectorHttpClient(TestCollectorEndpoint.Url, TimeSpan.Zero);

        var exception = act.Should().Throw<ArgumentOutOfRangeException>().Which;
        exception.ParamName.Should().Be("timeout");
    }

    [Fact]
    public async Task AddCollectorHttpClient_DefaultHttpClient_UsesConfiguredBaseAddress()
    {
        await using var provider = TestServiceCollectionFactory.Create(
                collectorUrl: TestCollectorEndpoint.Path("/root").TrimEnd('/'),
                timeout: TimeSpan.FromSeconds(12))
            .BuildServiceProvider();

        using var client = provider.GetRequiredService<HttpClient>();

        client.BaseAddress.Should().Be(new Uri(TestCollectorEndpoint.Path("/root")));
    }

    [Fact]
    public async Task AddCollectorHttpClient_DefaultHttpClient_AppliesConfiguredTimeoutThroughResilience()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static async (_, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        };

        await using var provider = TestServiceCollectionFactory.Create(
                handler,
                timeout: TimeSpan.FromMilliseconds(50))
            .BuildServiceProvider();

        using var client = provider.GetRequiredService<HttpClient>();

        var act = async () => await client.GetAsync("/api/v1/sessions", TestContext.Current.CancellationToken);

        var exception = (await act.Should().ThrowAsync<Polly.Timeout.TimeoutRejectedException>()).Which;
        exception.Message.Should().Contain("00:00:00.0500000");
    }

    [Fact]
    public async Task AddCollectorHttpClient_DefaultHttpClient_AddsApiKeyAndScopeToOutgoingRequests()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))
        };

        await using var provider = TestServiceCollectionFactory.Create(
                handler,
                apiKey: "test-token",
                serviceName: "planner",
                sessionId: "session-7")
            .BuildServiceProvider();

        using var client = provider.GetRequiredService<HttpClient>();
        using var response = await client.GetAsync("/api/v1/sessions?limit=5", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.LastApiKey.Should().Be("test-token");
        handler.LastAuthorization.Should().BeNull();
        handler.LastRequestUri.Should().Be(
            $"{TestCollectorEndpoint.Path("/api/v1/sessions")}?limit=5&service=planner&sessionId=session-7");
    }

    [Fact]
    public async Task AddCollectorToolClient_ResolvedTool_UsesCollectorConfiguredClient()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static (_, _) => Task.FromResult(JsonResponse(
                """
                {
                  "items": [
                    {
                      "session_id": "session-1",
                      "service_name": "planner",
                      "span_count": 3,
                      "error_count": 0,
                      "total_input_tokens": 12,
                      "total_output_tokens": 8,
                      "total_cost_usd": 0.0012,
                      "start_time": "2026-04-02T10:00:00Z"
                    }
                  ],
                  "total": 1
                }
                """))
        };

        var services = TestServiceCollectionFactory.Create(handler);
        services.AddCollectorToolClient<ReplayTools>();

        await using var provider = services.BuildServiceProvider();
        var tool = provider.GetRequiredService<ReplayTools>();

        var result = await tool.ListSessionsAsync(limit: 5, serviceName: "planner");

        result.Should().Contain("session-1");
        handler.LastRequestUri.Should().Be(
            $"{TestCollectorEndpoint.Path("/api/v1/sessions")}?limit=5&serviceName=planner");
    }

    [Fact]
    public async Task TelemetryStore_ResolvedFromServiceProvider_UsesCollectorConfiguredClient()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static (_, _) => Task.FromResult(JsonResponse(
                """
                {
                  "session_id": "run-1",
                  "service_name": "planner",
                  "span_count": 4,
                  "error_count": 0,
                  "total_input_tokens": 21,
                  "total_output_tokens": 13,
                  "total_cost_usd": 0.0021,
                  "start_time": "2026-04-02T10:00:00Z",
                  "end_time": "2026-04-02T10:00:03Z",
                  "providers": ["openai"],
                  "models": ["gpt-4o"]
                }
                """))
        };

        var services = TestServiceCollectionFactory.Create(handler);
        services.AddSingleton<ITelemetryStore, HttpTelemetryStore>();

        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<ITelemetryStore>();

        var run = await store.GetRunAsync("run-1");

        run.Should().NotBeNull();
        run!.RunId.Should().Be("run-1");
        handler.LastRequestUri.Should().Be(TestCollectorEndpoint.Path("/api/v1/sessions/run-1"));
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}
