using qyl.collector.Query;
using qyl.collector.Storage;

namespace qyl.collector.tests.Query;

/// <summary>
///     Integration tests for SessionQueryService - DuckDB session aggregation.
///     Tests VS-02 acceptance criteria: Session listing and GenAI stats aggregation.
/// </summary>
public sealed class SessionQueryServiceTests : IAsyncLifetime
{
    private SessionQueryService? _queryService;
    private DuckDbStore? _store;

    public async ValueTask InitializeAsync()
    {
        _store = DuckDbTestHelpers.CreateInMemoryStore();
        await DuckDbTestHelpers.WaitForSchemaInit();
        _queryService = new SessionQueryService(_store);
    }

    public ValueTask DisposeAsync() => _store?.DisposeAsync() ?? ValueTask.CompletedTask;

    private DuckDbStore Store => _store ?? throw new InvalidOperationException("Store not initialized");
    private SessionQueryService QueryService => _queryService ?? throw new InvalidOperationException("QueryService not initialized");

    #region GetSessionsAsync Tests

    [Fact]
    public async Task GetSessionsAsync_NoSpans_ReturnsEmptyList()
    {
        // Act
        var sessions = await QueryService.GetSessionsAsync();

        // Assert
        Assert.Empty(sessions);
    }

    [Fact]
    public async Task GetSessionsAsync_SingleSession_ReturnsAggregatedSession()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var batch = new SpanBatch(
        [
            SpanBuilder.GenAi("trace-1", "span-1")
                .WithSessionId("session-001")
                .WithName("chat-1")
                .AtTime(now)
                .WithTokens(100, 50)
                .WithCost(0.05)
                .Build(),
            SpanBuilder.GenAi("trace-1", "span-2")
                .WithSessionId("session-001")
                .WithName("chat-2")
                .AtTime(now, 150, 200)
                .WithTokens(200, 100)
                .WithCost(0.10)
                .Build()
        ]);

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, batch, TestConstants.LargeBatchProcessingDelayMs);

        // Act
        var sessions = await QueryService.GetSessionsAsync();

        // Assert
        Assert.Single(sessions);
        var session = sessions[0];
        Assert.Equal("session-001", session.SessionId);
        Assert.Equal(2, session.SpanCount);
        Assert.Equal(1, session.TraceCount);
        Assert.Equal(300, session.InputTokens); // 100 + 200
        Assert.Equal(150, session.OutputTokens); // 50 + 100
        Assert.Equal(450, session.TotalTokens);
        Assert.Equal(0.15, session.TotalCostUsd, 0.001);
        Assert.Equal(2, session.GenAiRequestCount);
    }

    [Fact]
    public async Task GetSessionsAsync_MultipleSessions_ReturnsAllOrderedByLastActivity()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        // Session 1 - older
        var batch1 = SpanBuilder.GenAi("trace-a", "span-a")
            .WithSessionId("session-old")
            .WithName("old-request")
            .AtTime(now.AddHours(-2))
            .WithTokens(50, 25)
            .Build();

        // Session 2 - newer
        var batch2 = SpanBuilder.GenAi("trace-b", "span-b")
            .WithSessionId("session-new")
            .WithName("new-request")
            .AtTime(now)
            .WithTokens(100, 50)
            .Build();

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, batch1);
        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, batch2);

        // Act
        var sessions = await QueryService.GetSessionsAsync();

        // Assert - ordered by last activity DESC
        Assert.Equal(2, sessions.Count);
        Assert.Equal("session-new", sessions[0].SessionId);
        Assert.Equal("session-old", sessions[1].SessionId);
    }

    [Fact]
    public async Task GetSessionsAsync_WithLimit_RespectsLimit()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        for (var i = 0; i < 5; i++)
        {
            var span = SpanBuilder.Create($"trace-{i}", $"span-{i}")
                .WithSessionId($"session-{i:D3}")
                .AtTime(now.AddMinutes(-i), 0, 50)
                .Build();
            await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, span);
        }

        // Act
        var sessions = await QueryService.GetSessionsAsync(3);

        // Assert
        Assert.Equal(3, sessions.Count);
    }

    [Fact]
    public async Task GetSessionsAsync_WithOffset_SkipsRecords()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        for (var i = 0; i < 5; i++)
        {
            var span = SpanBuilder.Create($"trace-{i}", $"span-{i}")
                .WithSessionId($"session-{i:D3}")
                .AtTime(now.AddMinutes(-i), 0, 50)
                .Build();
            await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, span);
        }

        // Act
        var sessions = await QueryService.GetSessionsAsync(10, 2);

        // Assert
        Assert.Equal(3, sessions.Count);
    }

    [Fact]
    public async Task GetSessionsAsync_WithAfterFilter_FiltersOldSessions()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        var oldSpan = SpanBuilder.Create("trace-old", "span-old")
            .WithSessionId("old-session")
            .AtTime(now.AddDays(-2), 0, 50)
            .Build();

        var newSpan = SpanBuilder.Create("trace-new", "span-new")
            .WithSessionId("new-session")
            .AtTime(now, 0, 50)
            .Build();

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, oldSpan);
        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, newSpan);

        // Act
        var sessions = await QueryService.GetSessionsAsync(after: now.AddDays(-1));

        // Assert
        Assert.Single(sessions);
        Assert.Equal("new-session", sessions[0].SessionId);
    }

    #endregion

    #region GetSessionAsync Tests

    [Fact]
    public async Task GetSessionAsync_ExistingSession_ReturnsSession()
    {
        // Arrange
        var span = SpanBuilder.GenAi("trace-1", "span-1")
            .WithSessionId("target-session")
            .WithTokens(150, 75)
            .WithCost(0.08)
            .Build();

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, span);

        // Act
        var session = await QueryService.GetSessionAsync("target-session");

        // Assert
        Assert.NotNull(session);
        Assert.Equal("target-session", session.SessionId);
        Assert.Equal(150, session.InputTokens);
        Assert.Equal(75, session.OutputTokens);
        Assert.Equal(0.08, session.TotalCostUsd, 0.001);
    }

    [Fact]
    public async Task GetSessionAsync_NonExistentSession_ReturnsNull()
    {
        // Act
        var session = await QueryService.GetSessionAsync("non-existent");

        // Assert
        Assert.Null(session);
    }

    [Fact]
    public async Task GetSessionAsync_SessionWithoutId_FallsBackToTraceId()
    {
        // Arrange - Span without session.id
        var span = SpanBuilder.Create("trace-fallback", "span-1")
            .WithSessionId(null)
            .Build();

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, span);

        // Act - Query using trace_id as fallback
        var session = await QueryService.GetSessionAsync("trace-fallback");

        // Assert
        Assert.NotNull(session);
        Assert.Equal("trace-fallback", session.SessionId);
    }

    #endregion

    #region GetSessionSpansAsync Tests

    [Fact]
    public async Task GetSessionSpansAsync_ReturnsSpansOrderedByTime()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var batch = new SpanBatch(
        [
            SpanBuilder.Create("trace-1", "span-3")
                .WithSessionId("session-timeline")
                .WithName("third")
                .AtTime(now, 200, 50)
                .Build(),
            SpanBuilder.Create("trace-1", "span-1")
                .WithSessionId("session-timeline")
                .WithName("first")
                .AtTime(now, 0, 50)
                .Build(),
            SpanBuilder.Create("trace-1", "span-2")
                .WithSessionId("session-timeline")
                .WithName("second")
                .AtTime(now, 100, 50)
                .Build()
        ]);

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, batch, TestConstants.LargeBatchProcessingDelayMs);

        // Act
        var spans = await QueryService.GetSessionSpansAsync("session-timeline");

        // Assert - ordered by start_time ASC
        Assert.Equal(3, spans.Count);
        Assert.Equal("first", spans[0].Name);
        Assert.Equal("second", spans[1].Name);
        Assert.Equal("third", spans[2].Name);
    }

    [Fact]
    public async Task GetSessionSpansAsync_WithLimit_RespectsLimit()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var spans = Enumerable.Range(0, 10)
            .Select(i => SpanBuilder.Create("trace-1", $"span-{i:D2}")
                .WithSessionId("session-limited")
                .WithName($"span-{i}")
                .AtTime(now, i * 10, 5)
                .Build())
            .ToList();

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, new SpanBatch(spans),
            TestConstants.LargeBatchProcessingDelayMs);

        // Act
        var result = await QueryService.GetSessionSpansAsync("session-limited", 5);

        // Assert
        Assert.Equal(5, result.Count);
    }

    #endregion

    #region GetGenAiStatsAsync Tests

    [Fact]
    public async Task GetGenAiStatsAsync_AggregatesCorrectly()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var batch = new SpanBatch(
        [
            SpanBuilder.GenAi("trace-1", "span-1")
                .WithSessionId("stats-session")
                .WithProvider("openai")
                .WithModel("gpt-4")
                .WithTokens(100, 50)
                .WithCost(0.05)
                .WithEval(0.9f)
                .AtTime(now)
                .Build(),
            SpanBuilder.GenAi("trace-1", "span-2")
                .WithSessionId("stats-session")
                .WithProvider("anthropic")
                .WithModel("claude-3")
                .WithTokens(200, 100)
                .WithCost(0.08)
                .WithEval(0.85f)
                .AtTime(now, 100)
                .Build(),
            // Non-GenAI span - should not be counted
            SpanBuilder.Create("trace-1", "span-3")
                .WithSessionId("stats-session")
                .WithProvider(null)
                .AtTime(now, 200, 50)
                .Build()
        ]);

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, batch, TestConstants.LargeBatchProcessingDelayMs);

        // Act
        var stats = await QueryService.GetGenAiStatsAsync("stats-session");

        // Assert
        Assert.Equal(2, stats.RequestCount); // Only GenAI spans
        Assert.Equal(300, stats.InputTokens);
        Assert.Equal(150, stats.OutputTokens);
        Assert.Equal(450, stats.TotalTokens);
        Assert.Equal(0.13, stats.TotalCostUsd);

        // Providers and models
        Assert.Contains("openai", stats.Providers);
        Assert.Contains("anthropic", stats.Providers);
        Assert.Contains("gpt-4", stats.Models);
        Assert.Contains("claude-3", stats.Models);
    }

    [Fact]
    public async Task GetGenAiStatsAsync_GlobalStats_AggregatesAllSessions()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var batch = new SpanBatch(
        [
            SpanBuilder.GenAi("trace-1", "span-1")
                .WithSessionId("session-1")
                .WithTokens(100, 50)
                .AtTime(now)
                .Build(),
            SpanBuilder.GenAi("trace-2", "span-2")
                .WithSessionId("session-2")
                .WithTokens(200, 100)
                .AtTime(now, 100)
                .Build()
        ]);

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, batch, TestConstants.LargeBatchProcessingDelayMs);

        // Act - no sessionId filter
        var stats = await QueryService.GetGenAiStatsAsync();

        // Assert
        Assert.Equal(2, stats.RequestCount);
        Assert.Equal(300, stats.InputTokens);
        Assert.Equal(150, stats.OutputTokens);
    }

    [Fact]
    public async Task GetGenAiStatsAsync_NoGenAiSpans_ReturnsEmptyStats()
    {
        // Arrange
        var span = SpanBuilder.Create("trace-1", "span-1")
            .WithSessionId("no-genai-session")
            .WithProvider(null)
            .Build();

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, span);

        // Act
        var stats = await QueryService.GetGenAiStatsAsync("no-genai-session");

        // Assert
        Assert.Equal(0, stats.RequestCount);
        Assert.Equal(0, stats.InputTokens);
        Assert.Equal(0, stats.OutputTokens);
    }

    #endregion

    #region GetTopModelsAsync Tests

    [Fact]
    public async Task GetTopModelsAsync_ReturnsModelsOrderedByCallCount()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var spans = new List<SpanStorageRow>();

        // gpt-4: 5 calls
        for (var i = 0; i < 5; i++)
            spans.Add(SpanBuilder.GenAi($"trace-gpt4-{i}", $"span-gpt4-{i}")
                .WithProvider("openai")
                .WithModel("gpt-4")
                .WithTokens(100, 50)
                .AtTime(now, i * 10, 50)
                .Build());

        // claude-3: 3 calls
        for (var i = 0; i < 3; i++)
            spans.Add(SpanBuilder.GenAi($"trace-claude-{i}", $"span-claude-{i}")
                .WithProvider("anthropic")
                .WithModel("claude-3")
                .WithTokens(150, 75)
                .AtTime(now, 100 + i * 10, 50)
                .Build());

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, new SpanBatch(spans),
            TestConstants.LargeBatchProcessingDelayMs);

        // Act
        var models = await QueryService.GetTopModelsAsync();

        // Assert
        Assert.Equal(2, models.Count);
        Assert.Equal("gpt-4", models[0].Model);
        Assert.Equal(5, models[0].CallCount);
        Assert.Equal("claude-3", models[1].Model);
        Assert.Equal(3, models[1].CallCount);
    }

    [Fact]
    public async Task GetTopModelsAsync_CalculatesTokensAndCost()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var batch = new SpanBatch(
        [
            SpanBuilder.GenAi("trace-1", "span-1")
                .WithProvider("openai")
                .WithModel("gpt-4")
                .WithTokens(100, 50)
                .WithCost(0.05)
                .AtTime(now) // 100ms
                .Build(),
            SpanBuilder.GenAi("trace-2", "span-2")
                .WithProvider("openai")
                .WithModel("gpt-4")
                .WithTokens(200, 100)
                .WithCost(0.10)
                .AtTime(now, 200, 200) // 200ms
                .Build()
        ]);

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, batch, TestConstants.LargeBatchProcessingDelayMs);

        // Act
        var models = await QueryService.GetTopModelsAsync();

        // Assert
        Assert.Single(models);
        var model = models[0];
        Assert.Equal("openai", model.Provider);
        Assert.Equal("gpt-4", model.Model);
        Assert.Equal(2, model.CallCount);
        Assert.Equal(300, model.InputTokens);
        Assert.Equal(150, model.OutputTokens);
        Assert.Equal(0.15, model.TotalCostUsd, 0.001);
        Assert.Equal(150, model.AvgLatencyMs, 1); // (100 + 200) / 2
    }

    #endregion

    #region GetErrorSummaryAsync Tests

    [Fact]
    public async Task GetErrorSummaryAsync_CalculatesErrorRate()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var batch = new SpanBatch(
        [
            SpanBuilder.Create("trace-1", "span-1")
                .WithSessionId("error-session")
                .WithStatusCode(1) // OK
                .AtTime(now, 0, 50)
                .Build(),
            SpanBuilder.Create("trace-1", "span-2")
                .WithSessionId("error-session")
                .WithStatusCode(2) // Error
                .AtTime(now, 100, 50)
                .Build(),
            SpanBuilder.Create("trace-1", "span-3")
                .WithSessionId("error-session")
                .WithStatusCode(2) // Error
                .AtTime(now, 200, 50)
                .Build(),
            SpanBuilder.Create("trace-1", "span-4")
                .WithSessionId("error-session")
                .WithStatusCode(1) // OK
                .AtTime(now, 300, 50)
                .Build()
        ]);

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, batch, TestConstants.LargeBatchProcessingDelayMs);

        // Act
        var summary = await QueryService.GetErrorSummaryAsync("error-session");

        // Assert
        Assert.Equal(4, summary.TotalSpans);
        Assert.Equal(2, summary.ErrorCount);
        Assert.Equal(50.0, summary.ErrorRate, 1); // 50%
    }

    [Fact]
    public async Task GetErrorSummaryAsync_NoErrors_ReturnsZeroRate()
    {
        // Arrange
        var span = SpanBuilder.Create("trace-1", "span-1")
            .WithSessionId("no-errors")
            .WithStatusCode(1) // OK
            .Build();

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, span);

        // Act
        var summary = await QueryService.GetErrorSummaryAsync("no-errors");

        // Assert
        Assert.Equal(1, summary.TotalSpans);
        Assert.Equal(0, summary.ErrorCount);
        Assert.Equal(0, summary.ErrorRate);
    }

    #endregion

    #region Session Duration and Timing Tests

    [Fact]
    public async Task GetSessionAsync_CalculatesDurationCorrectly()
    {
        // Arrange
        var startTime = TimeProvider.System.GetUtcNow().UtcDateTime;
        var batch = new SpanBatch(
        [
            SpanBuilder.Create("trace-1", "span-1")
                .WithSessionId("duration-session")
                .AtTime(startTime)
                .Build(),
            SpanBuilder.Create("trace-1", "span-2")
                .WithSessionId("duration-session")
                .AtTime(startTime, 500, 200)
                .Build()
        ]);

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, batch, TestConstants.LargeBatchProcessingDelayMs);

        // Act
        var session = await QueryService.GetSessionAsync("duration-session");

        // Assert
        Assert.NotNull(session);
        // Duration = last end_time - first start_time = (500+200) - 0 = 700ms
        Assert.Equal(700, session.DurationMs, 10);
    }

    [Fact]
    public async Task GetSessionsAsync_TracksModelList()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var batch = new SpanBatch(
        [
            SpanBuilder.GenAi("trace-1", "span-1")
                .WithSessionId("multi-model")
                .WithModel("gpt-4")
                .AtTime(now, 0, 50)
                .Build(),
            SpanBuilder.GenAi("trace-1", "span-2")
                .WithSessionId("multi-model")
                .WithModel("gpt-3.5-turbo")
                .AtTime(now, 100, 50)
                .Build(),
            SpanBuilder.GenAi("trace-1", "span-3")
                .WithSessionId("multi-model")
                .WithModel("gpt-4") // Duplicate
                .AtTime(now, 200, 50)
                .Build()
        ]);

        await DuckDbTestHelpers.EnqueueAndWaitAsync(Store, batch, TestConstants.LargeBatchProcessingDelayMs);

        // Act
        var session = await QueryService.GetSessionAsync("multi-model");

        // Assert
        Assert.NotNull(session);
        Assert.Equal(2, session.Models.Count); // Distinct models
        Assert.Contains("gpt-4", session.Models);
        Assert.Contains("gpt-3.5-turbo", session.Models);
    }

    #endregion
}
