using Qyl.Collector.Storage;

namespace Qyl.Collector.Integration.Tests.Storage;

/// <summary>
/// Real-DuckDB integration coverage for <see cref="DuckDbStore.InsertLogsAsync"/> and the
/// matching <see cref="DuckDbStore.GetLogsAsync"/> read path. Exercises the public log
/// adapter contract against an embedded, file-backed DuckDB database:
///
///   • the empty-batch early return must not commit anything,
///   • every persisted column on <see cref="LogStorageRow"/> must roundtrip through
///     <c>AddLogParameters</c> → <c>MapLog</c> (including the TINYINT severity number
///     and the auto-populated <c>created_at</c> default),
///   • <c>GetLogsAsync</c> orders by <c>time_unix_nano DESC</c>,
///   • each filter dimension (<c>severityText</c> exact equality, <c>minSeverity</c>
///     inclusive lower bound, <c>search</c> LIKE on body, asymmetric <c>after</c> &gt;
///     vs <c>before</c> &lt;= boundaries) selects the right subset,
///   • the <c>limit</c> parameter caps the returned row count after ordering, and
///   • a batch of <c>MaxLogsPerBatch + 1</c> rows commits across both chunks under a
///     single transaction so every row is visible afterwards.
///
/// Logs are a clean adapter for this coverage because the storage row's
/// <see cref="LogStorageRow.SeverityNumber"/> (<c>byte</c>) matches the schema's
/// <c>severity_number TINYINT NOT NULL</c> exactly — unlike the spans read path's
/// known <c>kind</c>/<c>status_code</c> VARCHAR-vs-byte mismatch documented in the
/// 2026-05-18 e2e routine notes.
/// </summary>
[Trait("Category", "Integration")]
public sealed class LogInsertAndQueryIntegrationTests : IAsyncDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"qyl-loginsert-{Guid.NewGuid():N}.duckdb");

    private readonly DuckDbStore _store;

    public LogInsertAndQueryIntegrationTests() => _store = new DuckDbStore(_dbPath);

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task Empty_batch_insert_is_a_noop_and_get_returns_no_rows()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.InsertLogsAsync([], ct);

        var rows = await _store.GetLogsAsync(ct: ct);

        rows.Should().BeEmpty(
            "the InsertLogsAsync empty-list early-return must not open a transaction or commit anything to the logs table");
    }

    [Fact]
    public async Task Single_log_roundtrips_every_persisted_column_through_the_reader()
    {
        var ct = TestContext.Current.CancellationToken;

        var log = new LogStorageRow
        {
            LogId = "log-roundtrip",
            TraceId = "trace-rt",
            SpanId = "span-rt",
            SessionId = "session-rt",
            TimeUnixNano = 1_700_000_000_000_000_000UL,
            ObservedTimeUnixNano = 1_700_000_000_000_000_500UL,
            SeverityNumber = 13,
            SeverityText = "WARN",
            Body = "db connection slow",
            ServiceName = "svc-api",
            AttributesJson = """{"db.system":"duckdb"}""",
            ResourceJson = """{"service.namespace":"qyl"}""",
            SourceFile = "Storage/DuckDbStore.cs",
            SourceLine = 42,
            SourceColumn = 7,
            SourceMethod = "InsertLogsAsync"
        };

        await _store.InsertLogsAsync([log], ct);

        var rows = await _store.GetLogsAsync(ct: ct);

        rows.Should().ContainSingle("only one log was inserted")
            .Which.Should().BeEquivalentTo(log, static opts => opts.Excluding(static r => r.CreatedAt),
                "every column written through AddLogParameters must roundtrip back via MapLog with the same value");

        rows[0].CreatedAt.Should().NotBeNull(
            "the schema declares created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP and MapLog reads ordinal 16 as a DateTimeOffset");
    }

    [Fact]
    public async Task GetLogsAsync_orders_results_by_time_unix_nano_descending()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.InsertLogsAsync(
            [
                MakeLog("log-early", time: 1_000UL),
                MakeLog("log-middle", time: 2_000UL),
                MakeLog("log-late", time: 3_000UL)
            ], ct);

        var rows = await _store.GetLogsAsync(ct: ct);

        rows.Select(static r => r.LogId).Should().ContainInOrder(["log-late", "log-middle", "log-early"],
            "the ORDER BY time_unix_nano DESC clause is the contract that drives the dashboard's most-recent-first feed");
    }

    [Fact]
    public async Task GetLogsAsync_severity_text_filter_is_exact_equality_not_a_prefix_match()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.InsertLogsAsync(
            [
                MakeLog("log-info", severityText: "INFO", severityNumber: 9),
                MakeLog("log-warn", severityText: "WARN", severityNumber: 13),
                MakeLog("log-error", severityText: "ERROR", severityNumber: 17)
            ], ct);

        var warnRows = await _store.GetLogsAsync(severityText: "WARN", ct: ct);

        warnRows.Should().ContainSingle("only one row has severity_text = 'WARN'")
            .Which.LogId.Should().Be("log-warn");

        var prefixRows = await _store.GetLogsAsync(severityText: "WAR", ct: ct);

        prefixRows.Should().BeEmpty(
            "severity_text is an equality filter (severity_text = $N) — a prefix term must not match");
    }

    [Fact]
    public async Task GetLogsAsync_min_severity_filter_is_inclusive_lower_bound_on_severity_number()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.InsertLogsAsync(
            [
                MakeLog("log-trace", severityNumber: 1),
                MakeLog("log-info", severityNumber: 9),
                MakeLog("log-warn", severityNumber: 13),
                MakeLog("log-error", severityNumber: 17)
            ], ct);

        var rows = await _store.GetLogsAsync(minSeverity: 13, ct: ct);

        rows.Select(static r => r.LogId).Should().BeEquivalentTo(["log-warn", "log-error"],
            "the >= 13 minSeverity filter must include the boundary row and every row with a higher severity_number; int → TINYINT parameter binding must work");
    }

    [Fact]
    public async Task GetLogsAsync_search_filter_is_substring_LIKE_match_on_body()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.InsertLogsAsync(
            [
                MakeLog("log-noop", body: "started up cleanly"),
                MakeLog("log-timeout-a", body: "db connection timeout after 30s"),
                MakeLog("log-timeout-b", body: "upstream timeout cascaded into retry storm")
            ], ct);

        var rows = await _store.GetLogsAsync(search: "timeout", ct: ct);

        rows.Select(static r => r.LogId).Should().BeEquivalentTo(["log-timeout-a", "log-timeout-b"],
            "the search filter wraps the term in '%term%' and runs body LIKE — both rows that contain 'timeout' must match");
    }

    [Fact]
    public async Task GetLogsAsync_after_is_exclusive_and_before_is_inclusive_at_the_boundary()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.InsertLogsAsync(
            [
                MakeLog("log-100", time: 100UL),
                MakeLog("log-200", time: 200UL),
                MakeLog("log-300", time: 300UL)
            ], ct);

        var afterRows = await _store.GetLogsAsync(after: 200UL, ct: ct);
        afterRows.Select(static r => r.LogId).Should().BeEquivalentTo(["log-300"],
            "after uses 'time_unix_nano > $N' so the boundary value (200) must be excluded");

        var beforeRows = await _store.GetLogsAsync(before: 200UL, ct: ct);
        beforeRows.Select(static r => r.LogId).Should().BeEquivalentTo(["log-100", "log-200"],
            "before uses 'time_unix_nano <= $N' so the boundary value (200) must be included");
    }

    [Fact]
    public async Task GetLogsAsync_limit_caps_returned_rows_after_DESC_ordering()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.InsertLogsAsync(
            [.. Enumerable.Range(0, 25).Select(i => MakeLog($"log-{i:D2}", time: 1_000UL + (ulong)i))],
            ct);

        var rows = await _store.GetLogsAsync(limit: 10, ct: ct);

        rows.Should().HaveCount(10, "the limit parameter must bound the row count after the ORDER BY");
        rows.Select(static r => r.LogId).Should().ContainInOrder(["log-24", "log-23", "log-22", "log-21", "log-20"],
            "the cap takes the 10 newest rows because the query orders by time_unix_nano DESC before applying the limit");
    }

    [Fact]
    public async Task Insert_batch_larger_than_MaxLogsPerBatch_chunks_under_a_single_transaction()
    {
        var ct = TestContext.Current.CancellationToken;

        const int total = 151;

        await _store.InsertLogsAsync(
            [.. Enumerable.Range(0, total).Select(i => MakeLog($"log-chunked-{i:D3}", time: 1_000_000UL + (ulong)i))],
            ct);

        var visible = await _store.GetLogCountAsync(ct);

        visible.Should().Be(total,
            "every row across both chunks must be visible — the InsertLogsAsync transaction commits all chunks atomically, and BuildMultiRowLogInsertSql must handle the chunk-size = 1 trailing batch");
    }

    private static LogStorageRow MakeLog(
        string logId,
        ulong time = 1_700_000_000_000_000_000UL,
        string severityText = "INFO",
        byte severityNumber = 9,
        string body = "integration body",
        string serviceName = "svc-default") =>
        new()
        {
            LogId = logId,
            TimeUnixNano = time,
            SeverityNumber = severityNumber,
            SeverityText = severityText,
            Body = body,
            ServiceName = serviceName
        };
}
