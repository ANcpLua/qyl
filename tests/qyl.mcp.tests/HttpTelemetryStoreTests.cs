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

        run.Should().NotBeNull();
        run!.RunId.Should().Be("run-42");
        run.AgentName.Should().Be("planner");
        run.Provider.Should().Be("openai");
        run.Model.Should().Be("gpt-4o");
        run.InputTokens.Should().Be(120);
        run.OutputTokens.Should().Be(30);
        run.Success.Should().BeFalse();
        run.ErrorType.Should().Be("Error");
        run.ErrorMessage.Should().Be("1 error(s)");
        run.Duration.Should().Be(TimeSpan.FromSeconds(3));
        handler.LastRequestUri.Should().Be(TestCollectorEndpoint.Path("/api/v1/sessions/run-42"));
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

        run.Should().BeNull();
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
            "openai",
            "gpt-4o",
            "Error",
            new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc));

        var run = results.Should().ContainSingle().Which;
        run.RunId.Should().Be("keep");
        handler.LastRequestUri.Should().Be(
            $"{TestCollectorEndpoint.Path("/api/v1/sessions")}?limit=100&provider=openai");
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
            "openai",
            "gpt-4o",
            "Error",
            null);

        results.Should().BeEmpty();
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
            new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 2, 23, 59, 59, DateTimeKind.Utc),
            "model");

        var summary = summaries.Should().ContainSingle().Which;
        summary.GroupKey.Should().Be("gpt-4o");
        summary.TotalInputTokens.Should().Be(16);
        summary.TotalOutputTokens.Should().Be(6);
        summary.RunCount.Should().Be(2);
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
        var summaries = await store.GetTokenUsageAsync(null, null, "service");

        summaries.Length.Should().Be(2);
        summaries.Should().Contain(static summary => summary.GroupKey == "planner" && summary.RunCount == 1);
        summaries.Should().Contain(static summary => summary.GroupKey == "coder" && summary.RunCount == 1);
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
        var summaries = await store.GetTokenUsageAsync(null, null, "model");

        summaries.Should().BeEmpty();
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
        var errors = await store.ListErrorsAsync(10, "planner");

        var error = errors.Should().ContainSingle().Which;
        error.RunId.Should().Be("error-run");
        error.ErrorType.Should().Be("Error");
        handler.LastRequestUri.Should().Be(
            $"{TestCollectorEndpoint.Path("/api/v1/sessions")}?limit=10&serviceName=planner");
    }

    [Fact]
    public async Task ListErrorsAsync_WhenCollectorRequestFails_ReturnsEmpty()
    {
        var handler = new RecordingHttpMessageHandler
        {
            Responder = static (_, _) => throw new HttpRequestException("boom")
        };

        var store = CreateStore(handler);
        var errors = await store.ListErrorsAsync(10, "planner");

        errors.Should().BeEmpty();
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
        var stats = await store.GetLatencyStatsAsync("planner", 2);

        stats.AgentName.Should().Be("planner");
        stats.P50Ms.Should().Be(20);
        stats.P95Ms.Should().Be(30);
        stats.P99Ms.Should().Be(30);
        stats.AvgMs.Should().Be(20);
        stats.MinMs.Should().Be(10);
        stats.MaxMs.Should().Be(30);
        stats.SampleCount.Should().Be(3);
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
        var stats = await store.GetLatencyStatsAsync("planner", 2);

        stats.AgentName.Should().Be("planner");
        stats.P50Ms.Should().Be(0);
        stats.P95Ms.Should().Be(0);
        stats.P99Ms.Should().Be(0);
        stats.AvgMs.Should().Be(0);
        stats.MinMs.Should().Be(0);
        stats.MaxMs.Should().Be(0);
        stats.SampleCount.Should().Be(0);
    }

    private static HttpTelemetryStore CreateStore(
        RecordingHttpMessageHandler handler,
        TimeProvider? timeProvider = null)
    {
        var client = new HttpClient(handler) { BaseAddress = TestCollectorEndpoint.BaseAddress };

        return new HttpTelemetryStore(
            client,
            timeProvider ?? TimeProvider.System,
            NullLogger<HttpTelemetryStore>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
