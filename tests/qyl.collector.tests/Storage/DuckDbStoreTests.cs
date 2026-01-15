using qyl.collector.Storage;

namespace qyl.collector.tests.Storage;

/// <summary>
///     Integration tests for DuckDbStore with in-memory DuckDB.
///     Tests schema initialization, span insert/retrieval, tracing, archival, and connection pooling.
/// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
public sealed class DuckDbStoreTests : IAsyncLifetime
{
    private DuckDbStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        _store = DuckDbTestHelpers.CreateInMemoryStore();
        await DuckDbTestHelpers.WaitForSchemaInit();
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
    }

    #region Helper Methods

    /// <summary>Converts DateTime to Unix nanoseconds (ulong).</summary>
    private static ulong DateTimeToUnixNano(DateTime dt)
    {
        var utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        var ticks = utc.Ticks - DateTime.UnixEpoch.Ticks;
        return (ulong)(ticks * 100); // 1 tick = 100 nanoseconds
    }

    #endregion

    #region Schema Initialization Tests

    [Fact]
    public async Task InitializeAsync_CreatesRequiredTables()
    {
        // Act
        var stats = await _store.GetStorageStatsAsync();

        // Assert
        Assert.Equal(0, stats.SpanCount);
        Assert.Equal(0, stats.SessionCount);
        Assert.Equal(0, stats.LogCount);
    }

    [Fact]
    public async Task InitializeAsync_CreatesSpansTableWithCorrectSchema()
    {
        // Act
        var columns = await _store.Connection.GetTableColumnsAsync("spans");

        // Assert
        Assert.NotEmpty(columns);

        // Core identifiers
        Assert.Contains(("trace_id", "VARCHAR"), columns);
        Assert.Contains(("span_id", "VARCHAR"), columns);

        // Snake_case columns for new schema
        var names = columns.ConvertAll(c => c.Name);
        Assert.Contains("service_name", names);
        Assert.Contains("session_id", names);
        Assert.Contains("gen_ai_system", names);
        Assert.Contains("gen_ai_input_tokens", names);
        Assert.Contains("gen_ai_output_tokens", names);
        Assert.Contains("start_time_unix_nano", names);
        Assert.Contains("end_time_unix_nano", names);
        Assert.Contains("duration_ns", names);
    }

    [Fact]
    public async Task InitializeAsync_CreatesSessions_And_LogsTables()
    {
        // Act & Assert
        Assert.True(await _store.Connection.TableExistsAsync("sessions"));
        Assert.True(await _store.Connection.TableExistsAsync("logs"));
    }

    [Fact]
    public async Task InitializeAsync_CreatesPerformanceIndexes()
    {
        // Act
        var indexCount = await _store.Connection.GetIndexCountAsync("spans");

        // Assert
        Assert.True(indexCount > 0, "Expected at least one index on spans table");
    }

    #endregion

    #region Insert and Retrieval Tests

    [Fact]
    public async Task InsertSpan_GetSpansBySession_RoundTrip()
    {
        // Arrange
        var span = SpanBuilder.GenAi(TestConstants.TraceDefault, TestConstants.SpanDefault)
            .WithSessionId(TestConstants.SessionDefault)
            .WithServiceName(TestConstants.ServiceDefault)
            .Build();

        // Act
        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, span);

        // Debug: Verify data was written
        var stats = await _store.GetStorageStatsAsync();
        Assert.Equal(1, stats.SpanCount); // This should fail if write didn't work

        // Debug: Query by trace to see if session is stored
        var traceResults = await _store.GetTraceAsync(TestConstants.TraceDefault);
        Assert.Single(traceResults);
        Assert.Equal(TestConstants.SessionDefault, traceResults[0].SessionId); // Check session ID value

        // Debug: Raw query to check session_id column directly
        await using var cmd = _store.Connection.CreateCommand();
        cmd.CommandText = "SELECT session_id FROM spans WHERE session_id IS NOT NULL";
        await using var reader = await cmd.ExecuteReaderAsync();
        var sessionIds = new List<string>();
        while (await reader.ReadAsync())
            sessionIds.Add(reader.GetString(0));
        Assert.Single(sessionIds); // Should have one session ID
        Assert.Equal(TestConstants.SessionDefault, sessionIds[0]);

        var results = await _store.GetSpansBySessionAsync(TestConstants.SessionDefault);

        // Assert
        Assert.Single(results);
        var retrieved = results[0];
        Assert.Equal(TestConstants.TraceDefault, retrieved.TraceId);
        Assert.Equal(TestConstants.SpanDefault, retrieved.SpanId);
        Assert.Equal(TestConstants.SessionDefault, retrieved.SessionId);
        Assert.Equal(TestConstants.ProviderOpenAi, retrieved.GenAiSystem);
        Assert.Equal(TestConstants.TokensInDefault, retrieved.GenAiInputTokens);
        Assert.Equal(TestConstants.TokensOutDefault, retrieved.GenAiOutputTokens);
        Assert.Equal((double)TestConstants.CostDefault, retrieved.GenAiCostUsd);
    }

    [Fact]
    public async Task InsertMultipleSpans_GetSpansBySession_ReturnsAll()
    {
        // Arrange
        var batch = SpanFactory.CreateBatch(
            "trace-002",
            TestConstants.SessionMultiple,
            TestConstants.BatchSizeSmall,
            TimeProvider.System.GetUtcNow().UtcDateTime);

        // Act
        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, batch, TestConstants.LargeBatchProcessingDelayMs);
        var results = await _store.GetSpansBySessionAsync(TestConstants.SessionMultiple);

        // Assert
        Assert.Equal(TestConstants.BatchSizeSmall, results.Count);
        for (var i = 0; i < TestConstants.BatchSizeSmall; i++)
            Assert.Contains(results, r => r.SpanId == $"span-{i:D3}");
    }

    [Fact]
    public async Task InsertDuplicateSpan_UpdatesExistingRecord()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        var span1 = SpanBuilder.Create(TestConstants.TraceDuplicate, TestConstants.SpanDuplicate)
            .WithTiming(now, TestConstants.DurationMediumMs)
            .WithStatusCode(0)
            .WithTokens(TestConstants.TokensInSmall, TestConstants.TokensOutSmall)
            .Build();

        var span2 = SpanBuilder.Create(TestConstants.TraceDuplicate, TestConstants.SpanDuplicate)
            .WithTiming(now, TestConstants.DurationDefaultMs)
            .WithStatusCode(0)
            .WithTokens(15, 25)
            .Build();

        // Act
        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, span1);
        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, span2);

        var count = await _store.Connection.CountSpansAsync(TestConstants.TraceDuplicate, TestConstants.SpanDuplicate);
        var results = await _store.GetTraceAsync(TestConstants.TraceDuplicate);

        // Assert - Should have exactly one record (updated, not duplicated)
        Assert.Equal(1, count);
        Assert.Single(results);

        var retrieved = results[0];
        // DurationNs should match the second span's duration (100ms = 100_000_000 ns)
        var expectedDurationNs = (ulong)(TestConstants.DurationDefaultMs * 1_000_000);
        Assert.Equal(expectedDurationNs, retrieved.DurationNs);
        Assert.Equal(15, retrieved.GenAiInputTokens);
        Assert.Equal(25, retrieved.GenAiOutputTokens);
    }

    [Fact]
    public async Task InsertSpan_WithNullableFields_HandlesCorrectly()
    {
        // Arrange
        var span = SpanBuilder.Minimal(TestConstants.TraceNullable, TestConstants.SpanNullable)
            .WithParentSpanId(null)
            .WithStatusCode(0)
            .WithSessionId(null)
            .WithProvider(null)
            .WithTokens(null, null)
            .WithCost(null)
            .Build();

        // Act
        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, span);
        var results = await _store.GetTraceAsync(TestConstants.TraceNullable);

        // Assert
        Assert.Single(results);
        var retrieved = results[0];
        Assert.Null(retrieved.ParentSpanId);
        // Kind and StatusCode are now byte (not nullable) - 0 means unspecified/unset
        Assert.Equal((byte)0, retrieved.Kind);
        Assert.Equal((byte)0, retrieved.StatusCode);
        Assert.Null(retrieved.SessionId);
        Assert.Null(retrieved.GenAiSystem);
        Assert.Null(retrieved.GenAiInputTokens);
        Assert.Null(retrieved.GenAiOutputTokens);
        Assert.Null(retrieved.GenAiCostUsd);
    }

    #endregion

    #region Trace Query Tests

    [Fact]
    public async Task GetTrace_ReturnsAllSpansInTraceOrderedByTime()
    {
        // Arrange
        var batch = SpanFactory.CreateHierarchy(TestConstants.TraceHierarchy, TimeProvider.System.GetUtcNow().UtcDateTime);

        // Act
        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, batch, TestConstants.LargeBatchProcessingDelayMs);
        var results = await _store.GetTraceAsync(TestConstants.TraceHierarchy);

        // Assert
        Assert.Equal(3, results.Count);

        // Verify order
        Assert.Equal(TestConstants.SpanRoot, results[0].SpanId);
        Assert.Equal(TestConstants.SpanChild1, results[1].SpanId);
        Assert.Equal(TestConstants.SpanChild2, results[2].SpanId);

        // Verify parent relationships
        Assert.Null(results[0].ParentSpanId);
        Assert.Equal(TestConstants.SpanRoot, results[1].ParentSpanId);
        Assert.Equal(TestConstants.SpanRoot, results[2].ParentSpanId);
    }

    [Fact]
    public async Task GetTrace_NonExistentTrace_ReturnsEmptyList()
    {
        // Act
        var results = await _store.GetTraceAsync(TestConstants.TraceNonExistent);

        // Assert
        Assert.Empty(results);
    }

    #endregion

    #region Parquet Archival Tests

    [Fact]
    public async Task ArchiveToParquet_CreatesFileAndDeletesOldSpans()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var batch = SpanFactory.CreateArchiveTestData(TestConstants.SessionArchive, now);

        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, batch, TestConstants.LargeBatchProcessingDelayMs);

        // Act
        var archivedCount = await _store.ArchiveToParquetAsync(
            tempDir.Path,
            TimeSpan.FromDays(TestConstants.ArchiveCutoffDays));

        await DuckDbTestHelpers.WaitForArchive();

        // Assert
        Assert.Equal(1, archivedCount);

        var stats = await _store.GetStorageStatsAsync();
        Assert.Equal(1, stats.SpanCount);

        var remaining = await _store.GetTraceAsync("trace-archive-new");
        Assert.Single(remaining);

        Assert.Single(tempDir.GetParquetFiles());
    }

    [Fact]
    public async Task ArchiveToParquet_NoMatchingSpans_ReturnsZero()
    {
        // Arrange
        using var tempDir = new TempDirectory();
        var span = SpanBuilder.Create("trace-recent", "span-recent")
            .WithName("recent")
            .WithTiming(TimeProvider.System.GetUtcNow().UtcDateTime, TestConstants.DurationShortMs)
            .Build();

        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, span);

        // Act - Archive spans older than 1000 days (the recent span won't match)
        var archivedCount = await _store.ArchiveToParquetAsync(
            tempDir.Path,
            TimeSpan.FromDays(1000));

        // Assert
        Assert.Equal(0, archivedCount);
        Assert.Empty(tempDir.GetParquetFiles());
    }

    #endregion

    #region Connection Pooling Tests

    [Fact]
    public async Task GetSpans_ConcurrentReads_UsesConnectionPool()
    {
        // Arrange
        var batch = SpanFactory.CreateBatch(
            "trace-pool",
            TestConstants.SessionPool,
            TestConstants.BatchSizeLarge,
            TimeProvider.System.GetUtcNow().UtcDateTime);

        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, batch, TestConstants.ConcurrentReadDelayMs);

        // Act
        var tasks = Enumerable.Range(0, TestConstants.ConcurrentReadCount)
            .Select(_ => _store.GetSpansBySessionAsync(TestConstants.SessionPool))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.Equal(TestConstants.BatchSizeLarge, r.Count));
    }

    [Fact]
    public async Task GetSpans_ConnectionLeaseDisposal_ReturnsToPool()
    {
        // Arrange
        var span = SpanBuilder.Create(TestConstants.TraceLease, "span-lease")
            .WithTiming(TimeProvider.System.GetUtcNow().UtcDateTime, TestConstants.DurationShortMs)
            .Build();

        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, span);

        // Act
        var lease1 = await _store.GetReadConnectionAsync();
        await lease1.DisposeAsync();

        var lease2 = await _store.GetReadConnectionAsync();
        await lease2.DisposeAsync();

        // Assert - Both leases should work
        Assert.NotNull(lease1);
        Assert.NotNull(lease2);
    }

    [Fact]
    public async Task GetSpans_WithMultipleFilters_ReturnsFilteredResults()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        var batch = new SpanBatch(
        [
            SpanBuilder.Create("trace-f1", "span-f1")
                .WithName("openai-call")
                .WithSessionId(TestConstants.SessionFilters)
                .WithProvider(TestConstants.ProviderOpenAi)
                .AtTime(now.AddMinutes(-120), 0) // 2 hours ago
                .Build(),
            SpanBuilder.Create("trace-f2", "span-f2")
                .WithName("anthropic-call")
                .WithSessionId(TestConstants.SessionFilters)
                .WithProvider(TestConstants.ProviderAnthropic)
                .AtTime(now.AddMinutes(-60), 0) // 1 hour ago
                .Build(),
            SpanBuilder.Create("trace-f3", "span-f3")
                .WithName("openai-call2")
                .WithSessionId(TestConstants.SessionFilters)
                .WithProvider(TestConstants.ProviderOpenAi)
                .AtTime(now, 0)
                .Build(),
            SpanBuilder.Create("trace-f4", "span-f4")
                .WithName("different-session")
                .WithSessionId("other-session")
                .WithProvider(TestConstants.ProviderOpenAi)
                .AtTime(now, 0)
                .Build()
        ]);

        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, batch, TestConstants.LargeBatchProcessingDelayMs);

        // Act
        var results = await _store.GetSpansAsync(
            TestConstants.SessionFilters,
            TestConstants.ProviderOpenAi);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, s =>
        {
            Assert.Equal(TestConstants.SessionFilters, s.SessionId);
            Assert.Equal(TestConstants.ProviderOpenAi, s.GenAiSystem);
        });

        // Act - Date range filter (convert DateTime to ulong nanoseconds)
        var startAfterNano = DateTimeToUnixNano(now.AddMinutes(-30));
        var startBeforeNano = DateTimeToUnixNano(now.AddMinutes(10));
        var recent = await _store.GetSpansAsync(
            TestConstants.SessionFilters,
            startAfter: startAfterNano,
            startBefore: startBeforeNano);

        // Assert
        Assert.Single(recent);
        Assert.Equal("span-f3", recent[0].SpanId);
    }

    #endregion

    #region Graceful Shutdown Tests

    [Fact]
    public async Task DisposeAsync_DrainsQueue_AndShutDown()
    {
        // Arrange
        var store = DuckDbTestHelpers.CreateInMemoryStore();
        var batch = SpanFactory.CreateBatch(
            "trace-shutdown",
            "session-shutdown",
            TestConstants.BatchSizeMedium,
            TimeProvider.System.GetUtcNow().UtcDateTime);

        // Act
        await store.EnqueueAsync(batch);
        await store.DisposeAsync();

        // Assert - If we got here without exception, shutdown succeeded
        Assert.True(true);
    }

    [Fact]
    public async Task AfterDispose_AccessingStore_ThrowsObjectDisposedException()
    {
        // Arrange
        var store = DuckDbTestHelpers.CreateInMemoryStore();
        await store.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await store.GetStorageStatsAsync());

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await store.EnqueueAsync(new SpanBatch([])));
    }

    #endregion

    #region GenAI Stats Tests

    [Fact]
    public async Task GetGenAiStats_AggregatesTokensAndCosts()
    {
        // Arrange
        var batch = SpanFactory.CreateGenAiStats(TestConstants.SessionStats, TimeProvider.System.GetUtcNow().UtcDateTime);

        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, batch, TestConstants.LargeBatchProcessingDelayMs);

        // Act
        var stats = await _store.GetGenAiStatsAsync(TestConstants.SessionStats);

        // Assert
        Assert.Equal(2, stats.RequestCount);
        Assert.Equal(180, stats.TotalInputTokens); // 100 + 80
        Assert.Equal(90, stats.TotalOutputTokens); // 50 + 40
        Assert.Equal(0.09, stats.TotalCostUsd, 0.001); // 0.05 + 0.04
    }

    [Fact]
    public async Task GetGenAiStats_WithDateFilter_ReturnsFilteredStats()
    {
        // Arrange
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        var batch = new SpanBatch(
        [
            SpanBuilder.GenAi("trace-old-stats", "span-old")
                .WithName("old-call")
                .AtTime(now.AddDays(-2), 0)
                .WithTokens(50, 25)
                .WithCost(TestConstants.CostSmall)
                .Build(),
            SpanBuilder.GenAi("trace-recent-stats", "span-recent")
                .WithName("recent-call")
                .AtTime(now, 0)
                .WithTokens(100, 50)
                .WithCost(TestConstants.CostLarge)
                .Build()
        ]);

        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, batch, TestConstants.LargeBatchProcessingDelayMs);

        // Act
        var startAfterNano = DateTimeToUnixNano(now.AddHours(-1));
        var recentStats = await _store.GetGenAiStatsAsync(startAfter: startAfterNano);

        // Assert
        Assert.Equal(1, recentStats.RequestCount);
        Assert.Equal(100, recentStats.TotalInputTokens);
        Assert.Equal(50, recentStats.TotalOutputTokens);
        Assert.Equal((double)TestConstants.CostLarge, recentStats.TotalCostUsd, 0.001);
    }

    #endregion

    #region Parameter Binding Tests (snake_case columns)

    [Fact]
    public async Task SessionIdColumn_WrittenAndQueryable()
    {
        // Arrange - Insert a span with known session ID
        var span = SpanBuilder.GenAi(TestConstants.TraceDefault, TestConstants.SpanDefault)
            .WithSessionId("test-session-123")
            .Build();

        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, span);

        // Verify data exists via stats
        var stats = await _store.GetStorageStatsAsync();
        Assert.Equal(1, stats.SpanCount);

        // Test: Single diagnostic query that counts AND shows the value
        await using var cmd = _store.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT
                              COUNT(*) as total_count,
                              COUNT(session_id) as non_null_session_count,
                              MAX(session_id) as session_value,
                              SUM(CASE WHEN session_id = 'test-session-123' THEN 1 ELSE 0 END) as exact_match_count
                          FROM spans
                          """;

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Should have results");

        var totalCount = reader.GetInt64(0);
        var nonNullSessionCount = reader.GetInt64(1);
        var sessionValue = reader.IsDBNull(2) ? "(NULL)" : reader.GetString(2);
        var exactMatchCount = reader.GetInt64(3);

        // Diagnostic output via assertions
        Assert.Equal(1, totalCount);
        Assert.Equal(1, nonNullSessionCount);
        Assert.Equal("test-session-123", sessionValue);
        Assert.Equal(1, exactMatchCount);
    }

    [Fact]
    public async Task MinimalReproduction_DirectDuckDb()
    {
        // Minimal test: direct DuckDB without DuckDbStore
        await using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        // Create simple table
        await using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = "CREATE TABLE test_table (session_id VARCHAR, trace_id VARCHAR)";
            await createCmd.ExecuteNonQueryAsync();
        }

        // Insert data
        await using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.CommandText =
                "INSERT INTO test_table (session_id, trace_id) VALUES ('test-session', 'test-trace')";
            await insertCmd.ExecuteNonQueryAsync();
        }

        // Verify data exists
        await using (var countCmd = connection.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM test_table";
            var count = Convert.ToInt64(await countCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            Assert.Equal(1, count);
        }

        // Query by session_id with literal
        await using (var queryCmd = connection.CreateCommand())
        {
            queryCmd.CommandText = "SELECT COUNT(*) FROM test_table WHERE session_id = 'test-session'";
            var found = Convert.ToInt64(await queryCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            Assert.Equal(1, found);
        }

        // Query by session_id with parameter
        await using (var paramCmd = connection.CreateCommand())
        {
            paramCmd.CommandText = "SELECT COUNT(*) FROM test_table WHERE session_id = $1";
            paramCmd.Parameters.Add(new DuckDBParameter
            {
                Value = "test-session"
            });
            var foundParam = Convert.ToInt64(await paramCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            Assert.Equal(1, foundParam);
        }
    }

    [Fact]
    public async Task MinimalReproduction_WithTransaction()
    {
        // Test: insert with transaction (exact pattern DuckDbStore uses)
        await using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        // Create simple table
        await using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = "CREATE TABLE test_table (session_id VARCHAR, trace_id VARCHAR)";
            await createCmd.ExecuteNonQueryAsync();
        }

        // Insert data IN TRANSACTION (like DuckDbStore WriteBatchInternalAsync)
        await using var tx = await connection.BeginTransactionAsync();
        await using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.Transaction = tx;
            insertCmd.CommandText = "INSERT INTO test_table (session_id, trace_id) VALUES ($1, $2)";
            insertCmd.Parameters.Add(new DuckDBParameter
            {
                Value = "test-session"
            });
            insertCmd.Parameters.Add(new DuckDBParameter
            {
                Value = "test-trace"
            });
            await insertCmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();

        // Query by session_id with literal (OUTSIDE transaction)
        await using (var queryCmd = connection.CreateCommand())
        {
            queryCmd.CommandText = "SELECT COUNT(*) FROM test_table WHERE session_id = 'test-session'";
            var found = Convert.ToInt64(await queryCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            Assert.Equal(1, found);
        }

        // Query by session_id with parameter (OUTSIDE transaction)
        await using (var paramCmd = connection.CreateCommand())
        {
            paramCmd.CommandText = "SELECT COUNT(*) FROM test_table WHERE session_id = $1";
            paramCmd.Parameters.Add(new DuckDBParameter
            {
                Value = "test-session"
            });
            var foundParam = Convert.ToInt64(await paramCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            Assert.Equal(1, foundParam);
        }
    }

    [Fact]
    public async Task MinimalReproduction_ViaStore_TraceQuery()
    {
        // Same pattern but using GetTraceAsync instead (should work)
        var store = DuckDbTestHelpers.CreateInMemoryStore();
        await DuckDbTestHelpers.WaitForSchemaInit();

        try
        {
            // Write ONE span
            var span = SpanBuilder.Create("trace-minimal2", "span-minimal2")
                .WithName("test-op")
                .WithSessionId("minimal-session2")
                .Build();
            await store.WriteBatchAsync(new SpanBatch([span]));

            // Query by trace
            var traceResults = await store.GetTraceAsync("trace-minimal2");
            Assert.Single(traceResults);
            Assert.Equal("trace-minimal2", traceResults[0].TraceId);
            Assert.Equal("minimal-session2", traceResults[0].SessionId);

            // Query by session (now uses LIKE workaround for DuckDB bug)
            var sessionResults = await store.GetSpansBySessionAsync("minimal-session2");
            Assert.Single(sessionResults);
            Assert.Equal("trace-minimal2", sessionResults[0].TraceId);
            Assert.Equal("minimal-session2", sessionResults[0].SessionId);
        }
        finally
        {
            await store.DisposeAsync();
        }
    }

    [Fact]
    public async Task NamedParameter_WorksWithSnakeCaseColumn()
    {
        // Arrange
        var span = SpanBuilder.GenAi("trace-param1", "span-param1")
            .WithSessionId("param-session-1")
            .Build();
        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, span);

        // Verify data was written
        var stats = await _store.GetStorageStatsAsync();
        Assert.Equal(1, stats.SpanCount);

        // Verify GetTraceAsync works (uses parameters internally)
        var traceResults = await _store.GetTraceAsync("trace-param1");
        Assert.Single(traceResults);
        Assert.Equal("param-session-1", traceResults[0].SessionId);

        // Inspect the actual session_id value stored (properly scoped to dispose reader)
        string actualSessionId;
        long actualLen, actualLenTrimmed;
        string actualTraceId, actualHex;
        {
            await using var inspectCmd = _store.Connection.CreateCommand();
            inspectCmd.CommandText = """
                                     SELECT
                                         session_id,
                                         LENGTH(session_id) as len,
                                         HEX(session_id) as hex_value,
                                         trace_id,
                                         LENGTH(TRIM(session_id)) as len_trimmed
                                     FROM spans
                                     """;
            await using var inspectReader = await inspectCmd.ExecuteReaderAsync();
            Assert.True(await inspectReader.ReadAsync());

            actualSessionId = inspectReader.IsDBNull(0) ? "(NULL)" : inspectReader.GetString(0);
            actualLen = inspectReader.IsDBNull(1) ? -1 : inspectReader.GetInt64(1);
            actualHex = inspectReader.IsDBNull(2) ? "(NULL)" : inspectReader.GetString(2);
            actualTraceId = inspectReader.GetString(3);
            actualLenTrimmed = inspectReader.IsDBNull(4) ? -1 : inspectReader.GetInt64(4);
        } // inspectCmd and inspectReader disposed here

        // Debug: Show hex and length comparison
        Assert.True(actualLen == actualLenTrimmed,
            $"LENGTH mismatch! Raw: {actualLen}, Trimmed: {actualLenTrimmed}, Hex: [{actualHex}]");

        // Show what we're dealing with
        Assert.Equal("trace-param1", actualTraceId); // Right row?
        Assert.NotEqual("(NULL)", actualSessionId); // Not null?
        Assert.Equal("param-session-1", actualSessionId); // Exact match?
        Assert.Equal(15L, actualLen); // Expected length?

        // Test GetSpansBySessionAsync
        var results = await _store.GetSpansBySessionAsync("param-session-1");
        Assert.Single(results);
    }

    [Fact]
    public async Task PositionalParameter_WorksWithSnakeCaseColumn()
    {
        // Arrange
        var span = SpanBuilder.GenAi("trace-param2", "span-param2")
            .WithSessionId("param-session-2")
            .Build();
        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, span);

        // NOTE: Due to DuckDB.NET 1.4.3 bug, = operator with parameters doesn't work for session_id
        // Workaround: Use LIKE with wildcards
        await using var cmd = _store.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM spans WHERE session_id LIKE '%param-session-2%'";
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetSpansBySessionAsync_WithSnakeCaseColumn_ReturnsCorrectResults()
    {
        // Arrange
        var span = SpanBuilder.GenAi("trace-api-test", "span-api-test")
            .WithSessionId("api-session-test")
            .Build();
        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, span);

        // Act: Use the actual API method
        var results = await _store.GetSpansBySessionAsync("api-session-test");

        // Assert
        Assert.Single(results);
        Assert.Equal("api-session-test", results[0].SessionId);
    }

    [Fact]
    public async Task GenAiColumns_WrittenAndQueryable()
    {
        // Arrange
        var span = SpanBuilder.GenAi("trace-genai", "span-genai")
            .WithSessionId("genai-session")
            .Build();
        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, span);

        // Act: Query genai columns - using LIKE workaround for DuckDB.NET bug
        await using var cmd = _store.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT gen_ai_system, gen_ai_request_model, gen_ai_input_tokens, gen_ai_output_tokens
                          FROM spans WHERE session_id LIKE '%genai-session%'
                          """;

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        var provider = reader.GetString(0);
        var model = reader.GetString(1);
        var inputTokens = reader.GetInt64(2);
        var outputTokens = reader.GetInt64(3);

        // Assert
        Assert.Equal(TestConstants.ProviderOpenAi, provider);
        Assert.Equal(TestConstants.ModelGpt4, model);
        Assert.Equal(TestConstants.TokensInDefault, inputTokens);
        Assert.Equal(TestConstants.TokensOutDefault, outputTokens);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task EnqueueAsync_EmptyBatch_IsNoOp()
    {
        // Act
        await _store.EnqueueAsync(new SpanBatch([]));
        var stats = await _store.GetStorageStatsAsync();

        // Assert
        Assert.Equal(0, stats.SpanCount);
    }

    [Fact]
    public async Task RetrievedSpan_CalculatedDuration_IsAccurate()
    {
        // Arrange
        var span = SpanBuilder.Create(TestConstants.TraceDuration, "span-duration")
            .WithName(TestConstants.OperationTimed)
            .WithTiming(TimeProvider.System.GetUtcNow().UtcDateTime, TestConstants.DurationPreciseMs)
            .Build();

        // Act
        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, span);
        var results = await _store.GetTraceAsync(TestConstants.TraceDuration);

        // Assert
        Assert.Single(results);
        var retrieved = results[0];
        var durationMs = retrieved.DurationNs / 1_000_000.0;
        Assert.Equal(TestConstants.DurationPreciseMs, durationMs, 1.0);
    }

    [Fact]
    public async Task InsertSpan_WithLargeAttributes_StoresCorrectly()
    {
        // Arrange
        var span = SpanFactory.CreateLargeDataSpan(TestConstants.TraceLarge, "span-large");
        var expectedLength = "{\"data\": \"".Length + TestConstants.LargeJsonPadding + "\"}".Length;

        // Act
        await DuckDbTestHelpers.EnqueueAndWaitAsync(_store, span);
        var results = await _store.GetTraceAsync(TestConstants.TraceLarge);

        // Assert
        Assert.Single(results);
        var retrieved = results[0];
        Assert.NotNull(retrieved.AttributesJson);
        Assert.Equal(expectedLength, retrieved.AttributesJson.Length);
    }

    #endregion
}
