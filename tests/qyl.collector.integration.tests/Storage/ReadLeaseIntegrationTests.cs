using System.Data;
using DuckDB.NET.Data;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Integration.Tests.Storage;

/// <summary>
/// Real-DuckDB integration coverage for <see cref="DuckDbStore.GetReadConnectionAsync"/>.
/// Exercises the lease against an embedded, file-backed DuckDB database. Verifies the
/// invariants the lease can actually deliver (distinct pooled read connection, working
/// SELECT path, gated concurrent acquisition) — see the matching PR body for the
/// documented contract gap around engine-level write rejection.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReadLeaseIntegrationTests : IAsyncDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"qyl-readlease-{Guid.NewGuid():N}.duckdb");

    private readonly DuckDbStore _store;

    public ReadLeaseIntegrationTests() =>
        _store = new DuckDbStore(_dbPath, maxConcurrentReads: 3);

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task Read_lease_returns_seeded_rows_via_select()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedSpanAsync("span-readable", ct);

        await using var lease = await _store.GetReadConnectionAsync(ct);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM spans WHERE span_id = 'span-readable'";

        var count = await ExecuteScalarLongAsync(cmd, ct);

        count.Should().Be(1);
    }

    [Fact]
    public async Task Read_lease_for_file_backed_store_does_not_short_circuit_to_writable_connection()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var lease = await _store.GetReadConnectionAsync(ct);

        lease.Connection.Should().NotBeSameAs(_store.Connection,
            "the in-memory short-circuit in ReadConnectionPolicy must not fire for a file-backed databasePath");
    }

    [Fact]
    public async Task Read_lease_observes_writes_committed_by_the_store_writer()
    {
        var ct = TestContext.Current.CancellationToken;

        await using (var leaseBefore = await _store.GetReadConnectionAsync(ct))
        {
            await using var probe = leaseBefore.Connection.CreateCommand();
            probe.CommandText = "SELECT COUNT(*) FROM spans WHERE span_id = 'span-visibility'";
            var before = await ExecuteScalarLongAsync(probe, ct);
            before.Should().Be(0);
        }

        await SeedSpanAsync("span-visibility", ct);

        await using var leaseAfter = await _store.GetReadConnectionAsync(ct);
        await using var cmd = leaseAfter.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM spans WHERE span_id = 'span-visibility'";
        var after = await ExecuteScalarLongAsync(cmd, ct);

        after.Should().Be(1,
            "the read lease must see writes committed through the store's serialized writer queue");
    }

    [Fact]
    public async Task Concurrent_read_leases_can_be_acquired_up_to_max_concurrent_reads()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedSpanAsync("span-concurrent", ct);

        var lease1 = await _store.GetReadConnectionAsync(ct);
        var lease2 = await _store.GetReadConnectionAsync(ct);
        var lease3 = await _store.GetReadConnectionAsync(ct);

        try
        {
            new[] { lease1.Connection, lease2.Connection, lease3.Connection }
                .Should().OnlyHaveUniqueItems(
                    "every concurrently-held lease should rent a distinct pooled DuckDBConnection");

            foreach (var lease in new[] { lease1, lease2, lease3 })
            {
                await using var cmd = lease.Connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM spans WHERE span_id = 'span-concurrent'";
                (await ExecuteScalarLongAsync(cmd, ct)).Should().Be(1);
            }
        }
        finally
        {
            await lease1.DisposeAsync();
            await lease2.DisposeAsync();
            await lease3.DisposeAsync();
        }
    }

    [Fact]
    public async Task Returned_lease_connection_can_be_rented_again_after_dispose()
    {
        var ct = TestContext.Current.CancellationToken;

        DuckDBConnection firstConnection;
        await using (var first = await _store.GetReadConnectionAsync(ct))
        {
            firstConnection = first.Connection;
            firstConnection.State.Should().Be(ConnectionState.Open);
        }

        await using var second = await _store.GetReadConnectionAsync(ct);

        second.Connection.Should().BeSameAs(firstConnection,
            "the pool should return the same DuckDBConnection once the first lease is disposed (LIFO retention)");
        second.Connection.State.Should().Be(ConnectionState.Open);
    }

    private Task SeedSpanAsync(string spanId, CancellationToken ct) =>
        _store.ExecuteWriteAsync(async (connection, token) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO spans
                                  (span_id, trace_id, name, kind,
                                   start_time_unix_nano, end_time_unix_nano, duration_ns,
                                   status_code)
                              VALUES ($1, 'trace-readlease', 'integration-seed', 'INTERNAL',
                                      0, 1000, 1000,
                                      'OK')
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = spanId });
            await cmd.ExecuteNonQueryAsync(token);
        }, ct);

    private static async Task<long> ExecuteScalarLongAsync(DuckDBCommand cmd, CancellationToken ct) =>
        await cmd.ExecuteScalarAsync(ct) switch
        {
            long l => l,
            int i => i,
            _ => 0L
        };
}
