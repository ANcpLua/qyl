using System.Data.Common;
using DuckDB.NET.Data;
using Qyl.Collector.Errors;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Integration.Tests.Storage;

/// <summary>
/// Real-DuckDB integration coverage for <see cref="DuckDbStore.UpsertErrorAsync"/> and the
/// <c>ErrorUpsertSql</c> ON CONFLICT clause it relies on. Exercises the upsert against an
/// embedded, file-backed DuckDB database to verify the contract the SQL is supposed to
/// honour:
///
///   • insert-or-fingerprint-merge semantics,
///   • set-style deduplication on <c>affected_services</c> and <c>sample_traces</c> (both
///     are <c>VARCHAR[]</c> in the schema),
///   • the 10-entry cap on <c>sample_traces</c>,
///   • <c>affected_users</c> NULL-aware <c>GREATEST</c> merge,
///   • <c>occurrence_count</c> incrementing once per conflict,
///   • <see cref="DuckDbStore.GetErrorsAsync"/> exposing the arrays as comma-joined strings
///     so the public <see cref="ErrorRow"/> shape is stable across the read path.
///
/// These scenarios would all bind-error under the original comma-string SQL because the
/// columns are <c>VARCHAR[]</c>, not <c>VARCHAR</c>; covering them here both pins the
/// fix in place and guards against future schema-vs-SQL drift.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ErrorUpsertIntegrationTests : IAsyncDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"qyl-errupsert-{Guid.NewGuid():N}.duckdb");

    private readonly DuckDbStore _store;

    public ErrorUpsertIntegrationTests() => _store = new DuckDbStore(_dbPath);

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task First_upsert_inserts_row_with_single_element_arrays_and_count_one()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.UpsertErrorAsync(MakeError("fp-fresh", "svc-a", "trace-aaa"), ct);

        var row = await ReadErrorRowAsync("fp-fresh", ct);

        row.OccurrenceCount.Should().Be(1, "first occurrence of a fingerprint seeds the counter");
        row.AffectedUsers.Should().BeNull(
            "AddErrorUpsertParameters binds DBNull when ErrorEvent.UserId is null or blank — see the GREATEST merge test for the populated case");
        row.AffectedServices.Should().BeEquivalentTo(["svc-a"]);
        row.SampleTraces.Should().BeEquivalentTo(["trace-aaa"]);
    }

    [Fact]
    public async Task Conflicting_upsert_with_new_service_merges_and_increments_count()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.UpsertErrorAsync(MakeError("fp-merge", "svc-a", "trace-aaa"), ct);
        await _store.UpsertErrorAsync(MakeError("fp-merge", "svc-b", "trace-bbb"), ct);

        var row = await ReadErrorRowAsync("fp-merge", ct);

        row.OccurrenceCount.Should().Be(2, "every conflicting insert bumps the counter exactly once");
        row.AffectedServices.Should().BeEquivalentTo(["svc-a", "svc-b"],
            "list_distinct(list_concat(...)) unions both services with set semantics (order is not guaranteed)");
        row.SampleTraces.Should().BeEquivalentTo(["trace-aaa", "trace-bbb"]);
    }

    [Fact]
    public async Task Conflicting_upsert_with_repeated_service_dedups_but_still_increments_count()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.UpsertErrorAsync(MakeError("fp-dedup", "svc-a", "trace-aaa"), ct);
        await _store.UpsertErrorAsync(MakeError("fp-dedup", "svc-a", "trace-aaa"), ct);
        await _store.UpsertErrorAsync(MakeError("fp-dedup", "svc-a", "trace-aaa"), ct);

        var row = await ReadErrorRowAsync("fp-dedup", ct);

        row.OccurrenceCount.Should().Be(3, "counter rises even when nothing new is contributed");
        row.AffectedServices.Should().BeEquivalentTo(["svc-a"],
            "repeated insertions of the same service must not produce duplicates");
        row.SampleTraces.Should().BeEquivalentTo(["trace-aaa"],
            "repeated insertions of the same trace id must not produce duplicates");
    }

    [Fact]
    public async Task Sample_traces_cap_holds_at_ten_entries()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 15; i++)
            await _store.UpsertErrorAsync(MakeError("fp-cap", $"svc-{i % 3}", $"trace-{i:D4}"), ct);

        var row = await ReadErrorRowAsync("fp-cap", ct);

        row.OccurrenceCount.Should().Be(15, "every insert is counted even when the trace cap drops the payload");
        row.SampleTraces.Should().HaveCount(10,
            "the CASE clause caps sample_traces at 10 entries to bound the row payload");
        row.AffectedServices.Should().BeEquivalentTo(["svc-0", "svc-1", "svc-2"],
            "affected_services has no cap — every distinct service should be retained");
    }

    [Fact]
    public async Task Affected_users_uses_greatest_with_null_aware_merge()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.UpsertErrorAsync(MakeError("fp-users", "svc-a", "trace-aaa", userId: null), ct);
        await _store.UpsertErrorAsync(MakeError("fp-users", "svc-a", "trace-aaa", userId: "u-1"), ct);

        var row = await ReadErrorRowAsync("fp-users", ct);

        row.AffectedUsers.Should().Be(1L,
            "the CASE picks GREATEST when both sides are set, and falls through to the non-NULL when one is missing");
    }

    [Fact]
    public async Task GetErrorsAsync_projects_arrays_as_comma_joined_strings_and_filters_by_service()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.UpsertErrorAsync(MakeError("fp-list-a", "svc-alpha", "trace-1"), ct);
        await _store.UpsertErrorAsync(MakeError("fp-list-a", "svc-beta", "trace-2"), ct);
        await _store.UpsertErrorAsync(MakeError("fp-list-b", "svc-gamma", "trace-3"), ct);

        var alphaRows = await _store.GetErrorsAsync(serviceName: "svc-alpha", ct: ct);

        alphaRows.Should().HaveCount(1, "the list_contains filter must select only rows whose array contains the value");
        alphaRows[0].Fingerprint.Should().Be("fp-list-a");

        var alphaServices = (alphaRows[0].AffectedServices ?? string.Empty).Split(',');
        alphaServices.Should().BeEquivalentTo(["svc-alpha", "svc-beta"],
            "array_to_string projects the VARCHAR[] back to the legacy comma-joined ErrorRow.AffectedServices shape");

        var alphaTraces = (alphaRows[0].SampleTraces ?? string.Empty).Split(',');
        alphaTraces.Should().BeEquivalentTo(["trace-1", "trace-2"]);

        var gammaRows = await _store.GetErrorsAsync(serviceName: "svc-gamma", ct: ct);
        gammaRows.Should().HaveCount(1);
        gammaRows[0].Fingerprint.Should().Be("fp-list-b");

        var missingRows = await _store.GetErrorsAsync(serviceName: "svc-does-not-exist", ct: ct);
        missingRows.Should().BeEmpty();
    }

    private async Task<RawErrorRow> ReadErrorRowAsync(string fingerprint, CancellationToken ct)
    {
        await using var lease = await _store.GetReadConnectionAsync(ct);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT occurrence_count, affected_users, affected_services, sample_traces
                          FROM errors WHERE fingerprint = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = fingerprint });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var read = await reader.ReadAsync(ct);
        read.Should().BeTrue($"no row was upserted under fingerprint '{fingerprint}'");

        return new RawErrorRow(
            reader.GetInt64(0),
            reader.IsDBNull(1) ? null : reader.GetInt64(1),
            ReadStringList(reader, 2),
            ReadStringList(reader, 3));
    }

    private static IReadOnlyList<string> ReadStringList(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return [];

        var raw = reader.GetValue(ordinal);
        return raw switch
        {
            IEnumerable<string> typed => [.. typed],
            IEnumerable<object?> objects => [.. objects.Select(static o => o?.ToString() ?? string.Empty)],
            _ => [raw.ToString() ?? string.Empty]
        };
    }

    private static ErrorEvent MakeError(string fingerprint, string service, string traceId, string? userId = null) =>
        new()
        {
            ErrorType = "System.InvalidOperationException",
            Message = "integration upsert scenario",
            Category = "exception",
            Fingerprint = fingerprint,
            ServiceName = service,
            TraceId = traceId,
            UserId = userId
        };

    private sealed record RawErrorRow(
        long OccurrenceCount,
        long? AffectedUsers,
        IReadOnlyList<string> AffectedServices,
        IReadOnlyList<string> SampleTraces);
}
