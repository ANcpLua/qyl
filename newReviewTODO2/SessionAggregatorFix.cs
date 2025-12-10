// =============================================================================
// SESSIONAGGREGATOR FIX OPTIONS
// 
// Option A: Remove unused _store field (if you don't need persistence bootstrap)
// Option B: Use _store to bootstrap sessions from DuckDB on startup
// =============================================================================

// =====================================
// OPTION A: Simple fix - remove _store
// =====================================

public sealed class SessionAggregator
{
    private readonly ConcurrentDictionary<string, SessionStats> _sessions = new();
    // REMOVED: private readonly DuckDbStore _store;

    public SessionAggregator()  // Remove DuckDbStore parameter
    {
        // No longer needs store reference
    }
    
    // ... rest of class unchanged
}

// Then update Program.cs DI registration:
// FROM: builder.Services.AddSingleton<SessionAggregator>();
// TO:   builder.Services.AddSingleton(_ => new SessionAggregator());

// =====================================
// OPTION B: Use _store to bootstrap sessions from existing data
// =====================================

public sealed class SessionAggregator
{
    private readonly ConcurrentDictionary<string, SessionStats> _sessions = new();
    private readonly DuckDbStore _store;
    private bool _initialized;

    public SessionAggregator(DuckDbStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Bootstraps session data from existing spans in DuckDB.
    /// Call this once during startup to recover state.
    /// </summary>
    public async Task InitializeFromStorageAsync(CancellationToken ct = default)
    {
        if (_initialized) return;
        
        // Query recent spans from storage and rebuild session stats
        var recentSpans = await _store.GetSpansAsync(
            startAfter: DateTime.UtcNow.AddHours(-24), // Last 24 hours
            limit: 10000,
            ct: ct);
        
        foreach (var span in recentSpans)
        {
            TrackSpan(span);
        }
        
        _initialized = true;
        Console.WriteLine($"[SessionAggregator] Bootstrapped {_sessions.Count} sessions from storage");
    }
    
    // ... rest of class unchanged
}

// Then call during startup in Program.cs:
// var aggregator = app.Services.GetRequiredService<SessionAggregator>();
// await aggregator.InitializeFromStorageAsync();

// =============================================================================
// FEEDBACK ENDPOINT FIX (Program.cs line 174)
// =============================================================================

// FROM (unused sessionId):
app.MapGet("/api/v1/sessions/{sessionId}/feedback", (string sessionId) =>
    Results.Ok(new { feedback = Array.Empty<object>() }));

// TO (use sessionId or mark as intentionally unused):
app.MapGet("/api/v1/sessions/{sessionId}/feedback", async (
    string sessionId,
    DuckDbStore store,
    CancellationToken ct) =>
{
    // TODO: Implement actual feedback retrieval
    var feedback = await store.GetFeedbackBySessionAsync(sessionId, ct);
    return Results.Ok(new FeedbackResponse(feedback.ToArray()));
});

// Or if keeping as placeholder, use discard pattern:
app.MapGet("/api/v1/sessions/{sessionId}/feedback", (string _) =>
    Results.Ok(new FeedbackResponse([])));

// =============================================================================
// await using FIXES (DuckDbStore.cs)
// =============================================================================

// Change all `using var cmd = ...` in async methods to `await using var cmd = ...`
// This ensures proper async disposal of DbCommand objects.

// Example fix for GetSpansBySessionAsync:

public async Task<IReadOnlyList<SpanRecord>> GetSpansBySessionAsync(string sessionId, CancellationToken ct = default)
{
    var spans = new List<SpanRecord>();

    await using var cmd = _connection.CreateCommand();  // <-- await using
    cmd.CommandText = """
        SELECT trace_id, span_id, parent_span_id, session_id,
               name, kind, start_time, end_time, status_code, status_message,
               provider_name, request_model, tokens_in, tokens_out,
               cost_usd, eval_score, eval_reason, attributes, events
        FROM spans
        WHERE session_id = $session_id
        ORDER BY start_time ASC
        """;
    cmd.Parameters.Add(new DuckDBParameter("session_id", sessionId));

    await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);  // <-- await using
    while (await reader.ReadAsync(ct).ConfigureAwait(false))
    {
        spans.Add(MapSpan(reader));
    }

    return spans;
}

// Apply same pattern to all async methods in DuckDbStore.cs:
// - GetTraceAsync (lines 195, 207)
// - GetSpansAsync (line 257)
// - QueryParquetAsync (lines 324, 333, 343)
// - GetStorageStatsAsync (lines 392, 400, 414)
// - GetFeedbackBySessionAsync (lines 424)
// - GetGenAiStatsAsync (lines 465, 482)
