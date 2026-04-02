namespace Qyl.Mcp.Tests;

public sealed class HttpTelemetryStoreTests
{
    [Fact]
    public async Task GetRunAsync_MapsCollectorSessionToAgentRun()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static (_, _) => Task.FromResult(JsonResponse(
                """
                {
                  "session_id": "run-42",
                  "service_name": "planner",
                  "span_count": 4,
                  "error_count": 1,
                  "total_input_tokens": 120,
                  "total_output_tokens": 30,
                  "total_cost_usd": 0.0042,
                  "start_time": "2026-04-02T10:00:00Z",
                  "end_time": "2026-04-02T10:00:03Z",
                  "providers": ["openai"],
                  "models": ["gpt-4o"]
                }
                """))
        };

        var store = CreateStore(handler);
        var run = await store.GetRunAsync("run-42");

        Assert.NotNull(run);
        Assert.Equal("run-42", run.RunId);
        Assert.Equal("planner", run.AgentName);
        Assert.Equal("openai", run.Provider);
        Assert.Equal("gpt-4o", run.Model);
        Assert.Equal(120, run.InputTokens);
        Assert.Equal(30, run.OutputTokens);
        Assert.False(run.Success);
        Assert.Equal("Error", run.ErrorType);
        Assert.Equal("1 error(s)", run.ErrorMessage);
        Assert.Equal(TimeSpan.FromSeconds(3), run.Duration);
        Assert.Equal(TestCollectorEndpoint.Path("/api/v1/sessions/run-42"), handler.LastRequestUri);
    }

    [Fact]
    public async Task GetRunAsync_WhenCollectorRequestFails_ReturnsNull()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static (_, _) => throw new HttpRequestException("boom")
        };

        var store = CreateStore(handler);
        var run = await store.GetRunAsync("missing-run");

        Assert.Null(run);
    }

    [Fact]
    public async Task SearchRunsAsync_FiltersModelErrorTypeAndSince()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static (_, _) => Task.FromResult(JsonResponse(
                """
                {
                  "items": [
                    {
                      "session_id": "keep",
                      "service_name": "planner",
                      "span_count": 3,
                      "error_count": 1,
                      "total_input_tokens": 50,
                      "total_output_tokens": 10,
                      "total_cost_usd": 0.001,
                      "start_time": "2026-04-02T10:00:00Z",
                      "end_time": "2026-04-02T10:00:01Z",
                      "providers": ["openai"],
                      "models": ["gpt-4o"]
                    },
                    {
                      "session_id": "wrong-model",
                      "service_name": "planner",
                      "span_count": 3,
                      "error_count": 1,
                      "total_input_tokens": 50,
                      "total_output_tokens": 10,
                      "total_cost_usd": 0.001,
                      "start_time": "2026-04-02T10:00:00Z",
                      "end_time": "2026-04-02T10:00:01Z",
                      "providers": ["openai"],
                      "models": ["gpt-4.1"]
                    },
                    {
                      "session_id": "too-old",
                      "service_name": "planner",
                      "span_count": 3,
                      "error_count": 1,
                      "total_input_tokens": 50,
                      "total_output_tokens": 10,
                      "total_cost_usd": 0.001,
                      "start_time": "2026-04-01T10:00:00Z",
                      "end_time": "2026-04-01T10:00:01Z",
                      "providers": ["openai"],
                      "models": ["gpt-4o"]
                    }
                  ],
                  "total": 3
                }
                """))
        };

        var store = CreateStore(handler);
        var results = await store.SearchRunsAsync(
            provider: "openai",
            model: "gpt-4o",
            errorType: "Error",
            since: new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc));

        var run = Assert.Single(results);
        Assert.Equal("keep", run.RunId);
        Assert.Equal(
            $"{TestCollectorEndpoint.Path("/api/v1/sessions")}?limit=100&provider=openai",
            handler.LastRequestUri);
    }

    [Fact]
    public async Task SearchRunsAsync_WhenCollectorRequestFails_ReturnsEmpty()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static (_, _) => throw new HttpRequestException("boom")
        };

        var store = CreateStore(handler);
        var results = await store.SearchRunsAsync(
            provider: "openai",
            model: "gpt-4o",
            errorType: "Error",
            since: null);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetTokenUsageAsync_GroupsByModelWithinRequestedRange()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static (_, _) => Task.FromResult(JsonResponse(
                """
                {
                  "items": [
                    {
                      "session_id": "inside-a",
                      "service_name": "planner",
                      "span_count": 1,
                      "error_count": 0,
                      "total_input_tokens": 10,
                      "total_output_tokens": 4,
                      "total_cost_usd": 0.001,
                      "start_time": "2026-04-02T10:15:00Z",
                      "end_time": "2026-04-02T10:16:00Z",
                      "providers": ["openai"],
                      "models": ["gpt-4o"]
                    },
                    {
                      "session_id": "inside-b",
                      "service_name": "planner",
                      "span_count": 1,
                      "error_count": 0,
                      "total_input_tokens": 6,
                      "total_output_tokens": 2,
                      "total_cost_usd": 0.001,
                      "start_time": "2026-04-02T10:30:00Z",
                      "end_time": "2026-04-02T10:31:00Z",
                      "providers": ["openai"],
                      "models": ["gpt-4o"]
                    },
                    {
                      "session_id": "outside",
                      "service_name": "planner",
                      "span_count": 1,
                      "error_count": 0,
                      "total_input_tokens": 99,
                      "total_output_tokens": 99,
                      "total_cost_usd": 0.001,
                      "start_time": "2026-04-03T12:00:00Z",
                      "end_time": "2026-04-03T12:01:00Z",
                      "providers": ["openai"],
                      "models": ["gpt-4.1"]
                    }
                  ],
                  "total": 3
                }
                """))
        };

        var store = CreateStore(handler);
        var summaries = await store.GetTokenUsageAsync(
            since: new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
            until: new DateTime(2026, 4, 2, 23, 59, 59, DateTimeKind.Utc),
            groupBy: "model");

        var summary = Assert.Single(summaries);
        Assert.Equal("gpt-4o", summary.GroupKey);
        Assert.Equal(16, summary.TotalInputTokens);
        Assert.Equal(6, summary.TotalOutputTokens);
        Assert.Equal(2, summary.RunCount);
    }

    [Fact]
    public async Task GetTokenUsageAsync_UsesServiceNameGroupingByDefault()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static (_, _) => Task.FromResult(JsonResponse(
                """
                {
                  "items": [
                    {
                      "session_id": "planner-run",
                      "service_name": "planner",
                      "span_count": 1,
                      "error_count": 0,
                      "total_input_tokens": 8,
                      "total_output_tokens": 3,
                      "total_cost_usd": 0.001,
                      "start_time": "2026-04-02T10:15:00Z",
                      "end_time": "2026-04-02T10:16:00Z",
                      "providers": ["openai"],
                      "models": ["gpt-4o"]
                    },
                    {
                      "session_id": "coder-run",
                      "service_name": "coder",
                      "span_count": 1,
                      "error_count": 0,
                      "total_input_tokens": 5,
                      "total_output_tokens": 2,
                      "total_cost_usd": 0.001,
                      "start_time": "2026-04-02T10:30:00Z",
                      "end_time": "2026-04-02T10:31:00Z",
                      "providers": ["openai"],
                      "models": ["gpt-4.1"]
                    }
                  ],
                  "total": 2
                }
                """))
        };

        var store = CreateStore(handler);
        var summaries = await store.GetTokenUsageAsync(since: null, until: null, groupBy: "service");

        Assert.Equal(2, summaries.Length);
        Assert.Contains(summaries, static summary => summary.GroupKey == "planner" && summary.RunCount == 1);
        Assert.Contains(summaries, static summary => summary.GroupKey == "coder" && summary.RunCount == 1);
    }

    [Fact]
    public async Task GetTokenUsageAsync_WhenCollectorReturnsNoSessions_ReturnsEmpty()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static (_, _) => Task.FromResult(JsonResponse(
                """
                {
                  "items": [],
                  "total": 0
                }
                """))
        };

        var store = CreateStore(handler);
        var summaries = await store.GetTokenUsageAsync(since: null, until: null, groupBy: "model");

        Assert.Empty(summaries);
    }

    [Fact]
    public async Task ListErrorsAsync_ReturnsErroredSessionsAndHonorsAgentNameFilter()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static (_, _) => Task.FromResult(JsonResponse(
                """
                {
                  "items": [
                    {
                      "session_id": "error-run",
                      "service_name": "planner",
                      "span_count": 3,
                      "error_count": 2,
                      "total_input_tokens": 5,
                      "total_output_tokens": 1,
                      "total_cost_usd": 0.001,
                      "start_time": "2026-04-02T10:00:00Z",
                      "end_time": "2026-04-02T10:01:00Z",
                      "providers": ["openai"],
                      "models": ["gpt-4o"]
                    },
                    {
                      "session_id": "healthy-run",
                      "service_name": "planner",
                      "span_count": 3,
                      "error_count": 0,
                      "total_input_tokens": 5,
                      "total_output_tokens": 1,
                      "total_cost_usd": 0.001,
                      "start_time": "2026-04-02T10:00:00Z",
                      "end_time": "2026-04-02T10:01:00Z",
                      "providers": ["openai"],
                      "models": ["gpt-4o"]
                    }
                  ],
                  "total": 2
                }
                """))
        };

        var store = CreateStore(handler);
        var errors = await store.ListErrorsAsync(limit: 10, agentName: "planner");

        var error = Assert.Single(errors);
        Assert.Equal("error-run", error.RunId);
        Assert.Equal("Error", error.ErrorType);
        Assert.Equal(
            $"{TestCollectorEndpoint.Path("/api/v1/sessions")}?limit=10&serviceName=planner",
            handler.LastRequestUri);
    }

    [Fact]
    public async Task ListErrorsAsync_WhenCollectorRequestFails_ReturnsEmpty()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static (_, _) => throw new HttpRequestException("boom")
        };

        var store = CreateStore(handler);
        var errors = await store.ListErrorsAsync(limit: 10, agentName: "planner");

        Assert.Empty(errors);
    }

    [Fact]
    public async Task GetLatencyStatsAsync_ComputesPercentilesFromSpanCounts()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static (_, _) => Task.FromResult(JsonResponse(
                """
                {
                  "items": [
                    {
                      "session_id": "run-a",
                      "service_name": "planner",
                      "span_count": 10,
                      "error_count": 0,
                      "total_input_tokens": 0,
                      "total_output_tokens": 0,
                      "total_cost_usd": 0.0,
                      "start_time": "2026-04-02T10:10:00Z",
                      "end_time": "2026-04-02T10:10:01Z",
                      "providers": [],
                      "models": []
                    },
                    {
                      "session_id": "run-b",
                      "service_name": "planner",
                      "span_count": 20,
                      "error_count": 0,
                      "total_input_tokens": 0,
                      "total_output_tokens": 0,
                      "total_cost_usd": 0.0,
                      "start_time": "2026-04-02T10:20:00Z",
                      "end_time": "2026-04-02T10:20:01Z",
                      "providers": [],
                      "models": []
                    },
                    {
                      "session_id": "run-c",
                      "service_name": "planner",
                      "span_count": 30,
                      "error_count": 0,
                      "total_input_tokens": 0,
                      "total_output_tokens": 0,
                      "total_cost_usd": 0.0,
                      "start_time": "2026-04-02T10:30:00Z",
                      "end_time": "2026-04-02T10:30:01Z",
                      "providers": [],
                      "models": []
                    }
                  ],
                  "total": 3
                }
                """))
        };

        var store = CreateStore(
            handler,
            new FixedTimeProvider(new DateTimeOffset(2026, 4, 2, 11, 0, 0, TimeSpan.Zero)));
        var stats = await store.GetLatencyStatsAsync("planner", hours: 2);

        Assert.Equal("planner", stats.AgentName);
        Assert.Equal(20, stats.P50Ms);
        Assert.Equal(30, stats.P95Ms);
        Assert.Equal(30, stats.P99Ms);
        Assert.Equal(20, stats.AvgMs);
        Assert.Equal(10, stats.MinMs);
        Assert.Equal(30, stats.MaxMs);
        Assert.Equal(3, stats.SampleCount);
    }

    [Fact]
    public async Task GetLatencyStatsAsync_WhenNoSessionsMatch_ReturnsZeroStats()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static (_, _) => Task.FromResult(JsonResponse(
                """
                {
                  "items": [
                    {
                      "session_id": "old-run",
                      "service_name": "planner",
                      "span_count": 10,
                      "error_count": 0,
                      "total_input_tokens": 0,
                      "total_output_tokens": 0,
                      "total_cost_usd": 0.0,
                      "start_time": "2026-04-01T08:00:00Z",
                      "end_time": "2026-04-01T08:00:01Z",
                      "providers": [],
                      "models": []
                    }
                  ],
                  "total": 1
                }
                """))
        };

        var store = CreateStore(
            handler,
            new FixedTimeProvider(new DateTimeOffset(2026, 4, 2, 11, 0, 0, TimeSpan.Zero)));
        var stats = await store.GetLatencyStatsAsync("planner", hours: 2);

        Assert.Equal("planner", stats.AgentName);
        Assert.Equal(0, stats.P50Ms);
        Assert.Equal(0, stats.P95Ms);
        Assert.Equal(0, stats.P99Ms);
        Assert.Equal(0, stats.AvgMs);
        Assert.Equal(0, stats.MinMs);
        Assert.Equal(0, stats.MaxMs);
        Assert.Equal(0, stats.SampleCount);
    }

    private static HttpTelemetryStore CreateStore(
        RecordingHttpMessageHandler handler,
        TimeProvider? timeProvider = null)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = TestCollectorEndpoint.BaseAddress
        };

        return new HttpTelemetryStore(
            client,
            timeProvider ?? TimeProvider.System,
            NullLogger<HttpTelemetryStore>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}
