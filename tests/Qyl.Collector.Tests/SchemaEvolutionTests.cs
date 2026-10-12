using DuckDB.NET.Data;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

/// <summary>
/// Contract 11.6.0 adds authoritative tables (annotations, saved queries, idempotency keys). A
/// database written by the 11.5.0 build holds telemetry, so the fail-closed schema check must let
/// exactly that additive change through and nothing else.
/// </summary>
public sealed class SchemaEvolutionTests
{
    // The authoritative identity every database written by the 11.5.0-era build stores.
    private const string TelemetryOnlyHash = "a20800b2a7d212fc3d284bf43b0ade1bf4dde1203f312623bad6a53d09911c19";

    private static readonly string[] s_telemetryTables = ["logs", "metric_points", "metric_series", "spans"];
    private static readonly string[] s_addedTables = ["analysis_state", "annotations", "idempotency_keys", "metric_exemplars", "saved_queries"];

    [Fact]
    public void The_identity_of_the_telemetry_tables_alone_is_the_previous_builds_identity()
    {
        Assert.Equal(TelemetryOnlyHash, DuckDbGeneratedSchema.ComputeAuthoritativeHash(s_telemetryTables));
        Assert.Equal(
            DuckDbGeneratedSchema.AuthoritativeHash,
            DuckDbGeneratedSchema.ComputeAuthoritativeHash(DuckDbGeneratedSchema.AuthoritativeTableNames));
        Assert.Equal(
            s_telemetryTables.Concat(s_addedTables).Order(StringComparer.Ordinal),
            DuckDbGeneratedSchema.AuthoritativeTableNames.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_previous_database_with_telemetry_opens_and_gains_the_resource_tables()
    {
        var path = await PreviousBuildDatabaseAsync(TelemetryOnlyHash);
        try
        {
            await using (var store = new DuckDbStore(path))
            {
                Assert.Single(await store.GetSpansAsync("alpha", ct: TestContext.Current.CancellationToken));
                Assert.Empty(await store.ListSavedQueriesAsync("alpha", null, null, 10,
                    TestContext.Current.CancellationToken));
            }

            using var con = Open(path);
            Assert.All(s_addedTables, table => Assert.True(TableExists(con, table)));
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT authoritative_schema_hash FROM qyl_schema_meta";
            Assert.Equal(DuckDbGeneratedSchema.AuthoritativeHash, cmd.ExecuteScalar());
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Theory]
    [InlineData("analysis_state")]
    [InlineData("metric_exemplars")]
    public async Task A_previous_resource_schema_gains_only_the_new_table(string newTable)
    {
        var present = DuckDbGeneratedSchema.AuthoritativeTableNames.Where(name => name != newTable).ToArray();
        var path = Path.Combine(Path.GetTempPath(), $"qyl-analysis-schema-{Guid.NewGuid():N}.duckdb");
        try
        {
            await using (var previous = new DuckDbStore(path))
                await previous.EnqueueAsync(new SpanBatch([CollectorApiTestHost.Span(CollectorApiTestHost.TraceId(1), CollectorApiTestHost.SpanId(1), 1, 2)]), TestContext.Current.CancellationToken);
            using (var con = Open(path))
            {
                using var command = con.CreateCommand();
                command.CommandText = "DROP TABLE " + newTable;
                command.ExecuteNonQuery();
                command.CommandText = "UPDATE qyl_schema_meta SET authoritative_schema_hash = $1";
                command.Parameters.Add(new DuckDBParameter { Value = DuckDbGeneratedSchema.ComputeAuthoritativeHash(present) });
                command.ExecuteNonQuery();
            }
            await using var store = new DuckDbStore(path);
            Assert.Single(await store.GetSpansAsync("alpha", ct: TestContext.Current.CancellationToken));
            var snapshot = await store.ReadAnalysisSnapshotAsync("alpha", ct: TestContext.Current.CancellationToken);
            Assert.Empty(snapshot.State);
            Assert.Empty(snapshot.Exemplars);
        }
        finally { DeleteDatabase(path); }
    }

    [Fact]
    public async Task An_unknown_stored_identity_still_fails_closed_while_telemetry_exists()
    {
        var path = await PreviousBuildDatabaseAsync(new string('0', 64));
        try
        {
            Assert.Throws<QylSchemaMismatchException>(() => new DuckDbStore(path));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    // Writes a database with today's build, then turns it into what the previous build left on
    // disk: no resource tables and the given stored identity, with one span of telemetry.
    private static async Task<string> PreviousBuildDatabaseAsync(string storedHash)
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-schema-{Guid.NewGuid():N}.duckdb");
        await using (var store = new DuckDbStore(path))
        {
            await store.EnqueueAsync(new SpanBatch([CollectorApiTestHost.Span(
                CollectorApiTestHost.TraceId(1), CollectorApiTestHost.SpanId(1), 1, 2)]),
                TestContext.Current.CancellationToken);
        }

        using var con = Open(path);
        foreach (var table in s_addedTables)
        {
            using var drop = con.CreateCommand();
            drop.CommandText = $"DROP TABLE \"{table}\"";
            drop.ExecuteNonQuery();
        }

        using var stamp = con.CreateCommand();
        stamp.CommandText = "UPDATE qyl_schema_meta SET authoritative_schema_hash = $1";
        stamp.Parameters.Add(new DuckDBParameter { Value = storedHash });
        stamp.ExecuteNonQuery();
        return path;
    }

    private static DuckDBConnection Open(string path)
    {
        var con = new DuckDBConnection($"DataSource={path}");
        con.Open();
        return con;
    }

    private static bool TableExists(DuckDBConnection con, string table)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM duckdb_tables() WHERE table_name = $1";
        cmd.Parameters.Add(new DuckDBParameter { Value = table });
        return Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) is 1;
    }

    private static void DeleteDatabase(string path)
    {
        File.Delete(path);
        File.Delete(path + ".wal");
    }
}
