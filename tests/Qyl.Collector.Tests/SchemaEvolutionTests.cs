using DuckDB.NET.Data;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

/// <summary>
/// A database written by an earlier build holds telemetry, so the fail-closed schema check must let
/// exactly the additive releases through and nothing else: tables the build adds (Contract 11.6.0
/// added annotations, saved queries and idempotency keys) are created empty, nullable columns the
/// build appends to a table (the span instrumentation scope) are added with NULL in every stored
/// row, a column a migration retires before the check (the idempotency keys' stored response) is
/// dropped, and any other difference refuses to open the database.
/// </summary>
[TestClass]
public sealed class SchemaEvolutionTests(TestContext testContext)
{
    private const string PreviousHash = "a20800b2a7d212fc3d284bf43b0ade1bf4dde1203f312623bad6a53d09911c19";
    // The authoritative schema identity 17.0.11 stamped, before its idempotency keys lost their stored responses.
    private const string Release17011Hash = "9051477eaf5c7f2d7ec7e897da2951829fa7d32bd20f7eceb1c1ceb93fade149";

    private static readonly string[] s_telemetryTables = ["logs", "metric_observations", "metric_series", "spans"];
    private static readonly string[] s_addedTables = ["analysis_state", "annotations", "idempotency_keys", "saved_queries"];
    private static readonly string[] s_derivedMetricTables = ["metric_exemplars", "metric_points"];
    private static readonly string[] s_appendedSpanColumns =
    [
        "scope_name", "scope_version", "scope_schema_url", "scope_attributes_json",
        "scope_dropped_attributes_count", "resource_schema_url", "resource_dropped_attributes_count",
        "trace_state", "flags", "dropped_attributes_count", "dropped_events_count", "dropped_links_count"
    ];

    [TestMethod]
    public void The_authoritative_tables_are_the_telemetry_tables_plus_the_added_ones()
    {
        Assert.AreSequenceEqual(
            s_telemetryTables.Concat(s_addedTables).Order(StringComparer.Ordinal),
            DuckDbGeneratedSchema.AuthoritativeTableNames.Order(StringComparer.Ordinal));
        Assert.AreSequenceEqual(s_derivedMetricTables,
            DuckDbGeneratedSchema.DerivedTableNames.Order(StringComparer.Ordinal));
    }

    [TestMethod]
    public async Task A_previous_database_with_telemetry_opens_and_gains_the_resource_tables()
    {
        var path = await PreviousBuildDatabaseAsync(PreviousHash);
        try
        {
            await using (var store = new DuckDbStore(path))
            {
                Assert.ContainsSingle(await store.GetSpansAsync("alpha", ct: testContext.CancellationToken));
                Assert.IsEmpty(await store.ListSavedQueriesAsync("alpha", null, null, 10,
                    testContext.CancellationToken));
            }

            await using var con = Open(path);
            s_addedTables.ToList().ForEach(table => Assert.IsTrue(TableExists(con, table)));
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT authoritative_schema_hash FROM qyl_schema_meta";
            Assert.AreEqual(DuckDbGeneratedSchema.AuthoritativeHash, cmd.ExecuteScalar());
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [TestMethod]
    [DataRow("analysis_state")]
    [DataRow("metric_exemplars")]
    public async Task A_previous_resource_schema_gains_only_the_new_table(string newTable)
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-analysis-schema-{Guid.NewGuid():N}.duckdb");
        try
        {
            await using (var previous = new DuckDbStore(path))
                await previous.EnqueueAsync(new SpanBatch([CollectorApiTestHost.Span(CollectorApiTestHost.TraceId(1), CollectorApiTestHost.SpanId(1), 1, 2)]), testContext.CancellationToken);
            await using (var con = Open(path))
            {
                Execute(con, "DROP TABLE " + newTable);
                Stamp(con, PreviousHash);
            }
            await using var store = new DuckDbStore(path);
            Assert.ContainsSingle(await store.GetSpansAsync("alpha", ct: testContext.CancellationToken));
            var snapshot = await store.ReadAnalysisSnapshotAsync("alpha", ct: testContext.CancellationToken);
            Assert.IsEmpty(snapshot.State);
            Assert.IsEmpty(snapshot.Exemplars);
        }
        finally { DeleteDatabase(path); }
    }

    [TestMethod]
    public async Task A_spans_table_without_the_scope_columns_gains_them_and_keeps_its_rows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-scope-schema-{Guid.NewGuid():N}.duckdb");
        try
        {
            await using (var previous = new DuckDbStore(path))
            {
                await previous.EnqueueAsync(new SpanBatch([CollectorApiTestHost.Span(CollectorApiTestHost.TraceId(1), CollectorApiTestHost.SpanId(1), 1, 2)]),
                    testContext.CancellationToken);
            }

            await using (var con = Open(path))
            {
                DropIndexes(con, "spans");
                foreach (var column in s_appendedSpanColumns.Reverse())
                    Execute(con, $"ALTER TABLE spans DROP COLUMN {column}");
                // The previous build's indexes are in place when the store opens, as on a live
                // database: ADD COLUMN must work beside them (dropping and recreating them in the
                // same transaction crashed DuckDB 1.5.6 with "pure virtual method called").
                Execute(con, SpanStorageRow.IndexesDdl);
                Stamp(con, PreviousHash);
            }

            await using (var store = new DuckDbStore(path))
            {
                var kept = Assert.ContainsSingle(await store.GetSpansAsync("alpha", ct: testContext.CancellationToken));
                Assert.IsNull(kept.ScopeName);
                Assert.IsNull(kept.ScopeVersion);

                await store.EnqueueAsync(new SpanBatch([CollectorApiTestHost.Span(CollectorApiTestHost.TraceId(2), CollectorApiTestHost.SpanId(2), 3, 4)
                    with { ScopeName = "Qyl.Telemetry.AutoInstrumentation", ScopeVersion = "3.1.0" }]), testContext.CancellationToken);
                var scoped = Assert.ContainsSingle(static span => span.ScopeName is not null,await store.GetSpansAsync("alpha", ct: testContext.CancellationToken));
                Assert.AreEqual("3.1.0", scoped.ScopeVersion);
            }

            await using (var con = Open(path))
            {
                await using var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT authoritative_schema_hash FROM qyl_schema_meta";
                Assert.AreEqual(DuckDbGeneratedSchema.AuthoritativeHash, cmd.ExecuteScalar());
                var expected = DuckDbGeneratedSchema.AuthoritativeTables.Single(static table => table.Name == "spans");
                Assert.AreEqual(expected.Indexes.Length, IndexCount(con, "spans"));
            }

            // Re-opening the migrated database is a plain match: nothing left to add.
            await using (var reopened = new DuckDbStore(path))
                Assert.HasCount(2, await reopened.GetSpansAsync("alpha", ct: testContext.CancellationToken));
        }
        finally { DeleteDatabase(path); }
    }

    /// <summary>A logs table stored before receipt times were recorded gains the column; its records stay readable with no receipt time.</summary>
    [TestMethod]
    public async Task A_logs_table_without_the_receipt_time_gains_it_and_keeps_its_rows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-receipt-schema-{Guid.NewGuid():N}.duckdb");
        try
        {
            await using (var previous = new DuckDbStore(path))
                await previous.InsertLogsAsync([new LogStorageRow { ProjectId = "alpha", LogId = "log-1", TimeUnixNano = 1, SeverityNumber = 9, Body = "kept" }],
                    testContext.CancellationToken);

            await using (var con = Open(path))
            {
                DropIndexes(con, "logs");
                Execute(con, "ALTER TABLE logs DROP COLUMN received_time_unix_nano");
                Execute(con, LogStorageRow.IndexesDdl);
                Stamp(con, PreviousHash);
            }

            await using (var store = new DuckDbStore(path))
            {
                var kept = Assert.ContainsSingle((await store.ReadAnalysisSnapshotAsync("alpha", ct: testContext.CancellationToken)).Logs);
                Assert.AreEqual("kept", kept.Body);
                Assert.IsNull(kept.ReceivedTimeUnixNano);
            }
        }
        finally { DeleteDatabase(path); }
    }

    /// <summary>
    /// 17.0.11 stored each create's first response beside its idempotency key, which nothing read once a repeat answered
    /// the record as it is now. A database it wrote, as the hosted volume is, opens with that column dropped: the key and
    /// the annotation it names stay, the same create repeated under the key still answers that annotation, and the
    /// database reopens as a plain match.
    /// </summary>
    [TestMethod]
    public async Task A_17_0_11_database_drops_its_stored_idempotency_responses_and_its_keys_still_answer_repeats()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-idempotency-schema-{Guid.NewGuid():N}.duckdb");
        var key = new Dictionary<string, string> { ["Idempotency-Key"] = "kept-retry" };
        var note = $$"""{"target":{"kind":"trace","trace_id":"{{CollectorApiTestHost.TraceId(1)}}"},"text":"kept"}""";
        try
        {
            string id;
            await using (var previous = await CollectorApiTestHost.StartAsync(testContext.CancellationToken, path))
            {
                await previous.Store.EnqueueAsync(new SpanBatch([CollectorApiTestHost.Span(CollectorApiTestHost.TraceId(1), CollectorApiTestHost.SpanId(1), 1, 2)]),
                    testContext.CancellationToken);
                using var created = await previous.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer", note, key);
                Assert.AreEqual(System.Net.HttpStatusCode.Created, created.StatusCode);
                id = (await previous.JsonAsync(created)).GetProperty("id").GetString()!;
            }

            await using (var con = Open(path))
            {
                // The table as 17.0.11 created it, its keys kept with the response each one stored.
                Execute(con, "DROP INDEX idx_idempotency_keys_created_at_unix_nano");
                Execute(con, "ALTER TABLE idempotency_keys RENAME TO idempotency_keys_current");
                Execute(con, """
                    CREATE TABLE idempotency_keys(project_id VARCHAR(128), operation VARCHAR(64), idempotency_key VARCHAR(255),
                        request_hash VARCHAR(64) NOT NULL, resource_id VARCHAR(128) NOT NULL, response_json JSON NOT NULL,
                        created_at_unix_nano UBIGINT NOT NULL, PRIMARY KEY(project_id, operation, idempotency_key))
                    """);
                Execute(con, """
                    INSERT INTO idempotency_keys SELECT project_id, operation, idempotency_key, request_hash, resource_id,
                        '{"stored":"response"}', created_at_unix_nano FROM idempotency_keys_current
                    """);
                Execute(con, "DROP TABLE idempotency_keys_current");
                Execute(con, "CREATE INDEX idx_idempotency_keys_created_at_unix_nano ON idempotency_keys(created_at_unix_nano)");
                Stamp(con, Release17011Hash);
            }

            await using (var host = await CollectorApiTestHost.StartAsync(testContext.CancellationToken, path))
            {
                using var repeated = await host.SendAsync(HttpMethod.Post, "/api/v1/annotations", "writer", note, key);
                Assert.AreEqual(System.Net.HttpStatusCode.Created, repeated.StatusCode);
                var answered = await host.JsonAsync(repeated);
                Assert.AreEqual((id, "kept"), (answered.GetProperty("id").GetString(), answered.GetProperty("text").GetString()));
                Assert.AreEqual(id, (await host.Store.GetIdempotencyKeyAsync(CollectorApiTestHost.AlphaProjectId, "annotations", "kept-retry", 0,
                    testContext.CancellationToken))?.ResourceId);
            }

            await using (var con = Open(path))
            {
                Assert.AreSequenceEqual(["project_id", "operation", "idempotency_key", "request_hash", "resource_id", "created_at_unix_nano"],
                    ColumnNames(con, "idempotency_keys"));
                Assert.AreEqual(1, IndexCount(con, "idempotency_keys"));
                await using var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT authoritative_schema_hash FROM qyl_schema_meta";
                Assert.AreEqual(DuckDbGeneratedSchema.AuthoritativeHash, cmd.ExecuteScalar());
            }

            // Re-opening the migrated database is a plain match: nothing left to drop.
            await using (var reopened = new DuckDbStore(path))
                Assert.AreEqual(id, (await reopened.GetAnnotationAsync(CollectorApiTestHost.AlphaProjectId, id, testContext.CancellationToken))?.AnnotationId);
        }
        finally { DeleteDatabase(path); }
    }

    [TestMethod]
    public async Task A_non_additive_difference_still_fails_closed_while_telemetry_exists()
    {
        var path = await PreviousBuildDatabaseAsync(PreviousHash);
        try
        {
            await using (var con = Open(path))
            {
                // A column removed from the middle of spans: the stored columns are no prefix of this build's.
                DropIndexes(con, "spans");
                Execute(con, "ALTER TABLE spans DROP COLUMN status_message");
                Execute(con, SpanStorageRow.IndexesDdl);
            }

            Assert.ThrowsExactly<QylSchemaMismatchException>(() => new DuckDbStore(path));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    // Writes a database with today's build, then turns it into what the previous build left on
    // disk: no resource tables and the given stored identity, with one span of telemetry.
    private async Task<string> PreviousBuildDatabaseAsync(string storedHash)
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-schema-{Guid.NewGuid():N}.duckdb");
        await using (var store = new DuckDbStore(path))
        {
            await store.EnqueueAsync(new SpanBatch([CollectorApiTestHost.Span(
                CollectorApiTestHost.TraceId(1), CollectorApiTestHost.SpanId(1), 1, 2)]),
                testContext.CancellationToken);
        }

        await using var con = Open(path);
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

    // DuckDB refuses to drop a column of a table an index depends on.
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

    private static List<string> ColumnNames(DuckDBConnection con, string table)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT column_name FROM duckdb_columns() WHERE table_name = $1 ORDER BY column_index";
        cmd.Parameters.Add(new DuckDBParameter { Value = table });
        using var reader = cmd.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
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
