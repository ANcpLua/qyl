using DuckDB.NET.Data;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

/// <summary>
/// A database written by an earlier build holds telemetry, so the fail-closed schema check must let
/// exactly the additive releases through and nothing else: tables the build adds (Contract 11.6.0
/// added annotations, saved queries and idempotency keys) are created empty, nullable columns the
/// build appends to a table (the span instrumentation scope) are added with NULL in every stored
/// row, and any other difference refuses to open the database.
/// </summary>
public sealed class SchemaEvolutionTests
{
    private const string PreviousHash = "a20800b2a7d212fc3d284bf43b0ade1bf4dde1203f312623bad6a53d09911c19";

    private static readonly string[] s_telemetryTables = ["logs", "metric_points", "metric_series", "spans"];
    private static readonly string[] s_addedTables = ["analysis_state", "annotations", "idempotency_keys", "metric_exemplars", "saved_queries"];
    private static readonly string[] s_appendedSpanColumns = ["scope_name", "scope_version"];

    [Fact]
    public void The_authoritative_tables_are_the_telemetry_tables_plus_the_added_ones()
    {
        Assert.Equal(
            s_telemetryTables.Concat(s_addedTables).Order(StringComparer.Ordinal),
            DuckDbGeneratedSchema.AuthoritativeTableNames.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_previous_database_with_telemetry_opens_and_gains_the_resource_tables()
    {
        var path = await PreviousBuildDatabaseAsync(PreviousHash);
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
        var path = Path.Combine(Path.GetTempPath(), $"qyl-analysis-schema-{Guid.NewGuid():N}.duckdb");
        try
        {
            await using (var previous = new DuckDbStore(path))
                await previous.EnqueueAsync(new SpanBatch([CollectorApiTestHost.Span(CollectorApiTestHost.TraceId(1), CollectorApiTestHost.SpanId(1), 1, 2)]), TestContext.Current.CancellationToken);
            using (var con = Open(path))
            {
                Execute(con, "DROP TABLE " + newTable);
                Stamp(con, PreviousHash);
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
    public async Task A_spans_table_without_the_scope_columns_gains_them_and_keeps_its_rows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-scope-schema-{Guid.NewGuid():N}.duckdb");
        try
        {
            await using (var previous = new DuckDbStore(path))
            {
                await previous.EnqueueAsync(new SpanBatch([CollectorApiTestHost.Span(CollectorApiTestHost.TraceId(1), CollectorApiTestHost.SpanId(1), 1, 2)]),
                    TestContext.Current.CancellationToken);
            }

            using (var con = Open(path))
            {
                DropIndexes(con, "spans");
                foreach (var column in s_appendedSpanColumns.Reverse())
                    Execute(con, $"ALTER TABLE spans DROP COLUMN {column}");
                Stamp(con, PreviousHash);
            }

            await using (var store = new DuckDbStore(path))
            {
                var kept = Assert.Single(await store.GetSpansAsync("alpha", ct: TestContext.Current.CancellationToken));
                Assert.Null(kept.ScopeName);
                Assert.Null(kept.ScopeVersion);

                await store.EnqueueAsync(new SpanBatch([CollectorApiTestHost.Span(CollectorApiTestHost.TraceId(2), CollectorApiTestHost.SpanId(2), 3, 4)
                    with { ScopeName = "Qyl.Telemetry.AutoInstrumentation", ScopeVersion = "3.1.0" }]), TestContext.Current.CancellationToken);
                var scoped = Assert.Single(await store.GetSpansAsync("alpha", ct: TestContext.Current.CancellationToken), static span => span.ScopeName is not null);
                Assert.Equal("3.1.0", scoped.ScopeVersion);
            }

            using (var con = Open(path))
            {
                using var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT authoritative_schema_hash FROM qyl_schema_meta";
                Assert.Equal(DuckDbGeneratedSchema.AuthoritativeHash, cmd.ExecuteScalar());
                var expected = DuckDbGeneratedSchema.AuthoritativeTables.Single(static table => table.Name == "spans");
                Assert.Equal(expected.Indexes.Length, IndexCount(con, "spans"));
            }

            // Re-opening the migrated database is a plain match: nothing left to add.
            await using (var reopened = new DuckDbStore(path))
                Assert.Equal(2, (await reopened.GetSpansAsync("alpha", ct: TestContext.Current.CancellationToken)).Count);
        }
        finally { DeleteDatabase(path); }
    }

    [Fact]
    public async Task A_non_additive_difference_still_fails_closed_while_telemetry_exists()
    {
        var path = await PreviousBuildDatabaseAsync(PreviousHash);
        try
        {
            using (var con = Open(path))
            {
                // A column removed from the middle of spans: the stored columns are no prefix of this build's.
                DropIndexes(con, "spans");
                Execute(con, "ALTER TABLE spans DROP COLUMN status_message");
            }

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
            Execute(con, $"DROP TABLE \"{table}\"");
        Stamp(con, storedHash);
        return path;
    }

    private static DuckDBConnection Open(string path)
    {
        var con = new DuckDBConnection($"DataSource={path}");
        con.Open();
        return con;
    }

    private static void Execute(DuckDBConnection con, string sql)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void Stamp(DuckDBConnection con, string storedHash)
    {
        using var stamp = con.CreateCommand();
        stamp.CommandText = "UPDATE qyl_schema_meta SET authoritative_schema_hash = $1";
        stamp.Parameters.Add(new DuckDBParameter { Value = storedHash });
        stamp.ExecuteNonQuery();
    }

    // DuckDB refuses to alter a table an index depends on.
    private static void DropIndexes(DuckDBConnection con, string table)
    {
        var names = new List<string>();
        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = "SELECT index_name FROM duckdb_indexes() WHERE table_name = $1";
            cmd.Parameters.Add(new DuckDBParameter { Value = table });
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                names.Add(reader.GetString(0));
        }

        foreach (var name in names)
            Execute(con, $"DROP INDEX \"{name}\"");
    }

    private static int IndexCount(DuckDBConnection con, string table)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM duckdb_indexes() WHERE table_name = $1";
        cmd.Parameters.Add(new DuckDBParameter { Value = table });
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
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
