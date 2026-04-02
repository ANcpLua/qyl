namespace Qyl.Mcp.Tests;

public sealed class McpCollectorHttpClientExtensionsTests
{
    [Fact]
    public void AddCollectorHttpClient_InvalidCollectorUrl_ThrowsInvalidOperationException()
    {
        ServiceCollection services = [];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddCollectorHttpClient("collector-without-scheme"));

        Assert.Equal("Invalid QYL_COLLECTOR_URL 'collector-without-scheme'.", exception.Message);
    }

    [Fact]
    public void AddCollectorHttpClient_NonPositiveTimeout_ThrowsArgumentOutOfRangeException()
    {
        ServiceCollection services = [];

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddCollectorHttpClient(TestCollectorEndpoint.Url, TimeSpan.Zero));

        Assert.Equal("timeout", exception.ParamName);
    }

    [Fact]
    public async Task AddCollectorHttpClient_DefaultHttpClient_UsesConfiguredBaseAddress()
    {
        await using var provider = TestServiceCollectionFactory.Create(
                collectorUrl: TestCollectorEndpoint.Path("/root").TrimEnd('/'),
                timeout: TimeSpan.FromSeconds(12))
            .BuildServiceProvider();

        using var client = provider.GetRequiredService<HttpClient>();

        Assert.Equal(new Uri(TestCollectorEndpoint.Path("/root")), client.BaseAddress);
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

        var exception = await Assert.ThrowsAsync<Polly.Timeout.TimeoutRejectedException>(async () =>
            await client.GetAsync("/api/v1/sessions", TestContext.Current.CancellationToken));

        Assert.Contains("00:00:00.0500000", exception.Message, StringComparison.Ordinal);
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("test-token", handler.LastApiKey);
        Assert.Null(handler.LastAuthorization);
        Assert.Equal(
            $"{TestCollectorEndpoint.Path("/api/v1/sessions")}?limit=5&service=planner&sessionId=session-7",
            handler.LastRequestUri);
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

        Assert.Contains("session-1", result, StringComparison.Ordinal);
        Assert.Equal(
            $"{TestCollectorEndpoint.Path("/api/v1/sessions")}?limit=5&serviceName=planner",
            handler.LastRequestUri);
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

        Assert.NotNull(run);
        Assert.Equal("run-1", run.RunId);
        Assert.Equal(TestCollectorEndpoint.Path("/api/v1/sessions/run-1"), handler.LastRequestUri);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}
