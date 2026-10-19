using System.Globalization;
using System.Net;
using System.Text;
using DuckDB.NET.Data;
using Qyl.Collector.Storage;
using static Qyl.Collector.Tests.CollectorApiTestHost;

namespace Qyl.Collector.Tests;

/// <summary>
/// One series timestamp holds one measurement. An identical export retry changes nothing; a different
/// measurement never replaces the first one recorded: both stay in the journal, the point keeps the
/// first, and check_telemetry_health reports the conflict, before and after the database reopens.
/// </summary>
[TestClass]
public sealed class MetricConflictPersistenceTests(TestContext testContext)
{
    private const ulong Minute = 60_000_000_000;
    private static readonly string[] s_tables = ["metric_series", "metric_observations", "metric_points", "metric_exemplars"];

    [TestMethod]
    public async Task An_identical_retry_leaves_every_stored_row_unchanged()
    {
        var path = Path.Combine(testContext.TestTempDirectory!, "retry.duckdb");
        var (series, first) = Gauge(42);
        await using (var store = new DuckDbStore(path)) await store.InsertMetricsAsync([series], [first], testContext.CancellationToken);
        var before = Rows(path);

        await using (var store = new DuckDbStore(path)) await store.InsertMetricsAsync([series], [first], testContext.CancellationToken);

        Assert.AreEqual(before, Rows(path));
        Assert.Contains(" has_conflict=0", Assert.ContainsSingle(static row => row.StartsWith("metric_points ", StringComparison.Ordinal), before.Split('\n')), StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task A_conflicting_measurement_keeps_the_first_and_is_reported(bool sameExport)
    {
        var path = Path.Combine(testContext.TestTempDirectory!, $"conflict-{sameExport}.duckdb");
        var (series, first) = Gauge(42);
        var (_, second) = Gauge(43);
        await using (var host = await StartAsync(testContext.CancellationToken, path))
        {
            if (sameExport) await host.Store.InsertMetricsAsync([series], [first, second], testContext.CancellationToken);
            else
            {
                await host.Store.InsertMetricsAsync([series], [first], testContext.CancellationToken);
                await host.Store.InsertMetricsAsync([series], [second], testContext.CancellationToken);
            }
            await AssertReportedAsync(host);
        }

        var rows = Rows(path).Split('\n');
        Assert.HasCount(2, rows.Where(static row => row.StartsWith("metric_observations ", StringComparison.Ordinal)));
        var point = Assert.ContainsSingle(static row => row.StartsWith("metric_points ", StringComparison.Ordinal), rows);
        Assert.Contains(" value=42 ", point, StringComparison.Ordinal);
        Assert.Contains(" has_conflict=1", point, StringComparison.Ordinal);
        await using var reopened = await StartAsync(testContext.CancellationToken, path);
        await AssertReportedAsync(reopened);
        using var refused = await reopened.SendAsync(HttpMethod.Post, "/api/v1/analysis/query_metric", ReaderApiKey, """{"metric_name":"queue.depth","step_ms":3600000}""");
        Assert.AreEqual(HttpStatusCode.Conflict, refused.StatusCode);
    }

    private static async Task AssertReportedAsync(CollectorApiTestHost host)
    {
        using var response = await host.SendAsync(HttpMethod.Post, "/api/v1/analysis/check_telemetry_health", ReaderApiKey, "{}");
        var health = await host.JsonAsync(response);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, health.GetRawText());
        var conflicts = health.GetProperty("metric_conflicts");
        Assert.AreEqual(1, conflicts.GetProperty("points").GetInt32());
        var conflict = Assert.ContainsSingle(conflicts.GetProperty("series").EnumerateArray());
        Assert.AreEqual(("queue.depth", "s_queue", 1), (conflict.GetProperty("metric_name").GetString(), conflict.GetProperty("series_id").GetString(), conflict.GetProperty("points").GetInt32()));
        Assert.AreEqual(FixtureUtcNow.AddMinutes(-10), conflict.GetProperty("first_time").GetDateTimeOffset());
    }

    private static (MetricSeriesRow Series, MetricObservationRow Observation) Gauge(double value)
    {
        var time = FixtureUnixNano - 10 * Minute;
        var series = new MetricSeriesRow
        {
            ProjectId = AlphaProjectId, SeriesId = "s_queue", MetricName = "queue.depth", MetricKind = 1, Temporality = 0, IsMonotonic = 0,
            FirstSeenUnixNano = time, LastSeenUnixNano = time, ServiceName = "checkout"
        };
        return (series, MetricFixtures.Observation(series, new MetricPointRow { ProjectId = AlphaProjectId, SeriesId = "s_queue", TimeUnixNano = time, Value = value }));
    }

    // Every stored row of the metric tables, read after the store released the file.
    private static string Rows(string path)
    {
        var output = new StringBuilder();
        using var connection = new DuckDBConnection($"DataSource={path}");
        connection.Open();
        foreach (var table in s_tables)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM {table} ORDER BY ALL";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                output.Append(table);
                for (var column = 0; column < reader.FieldCount; column++)
                    output.Append(' ').Append(reader.GetName(column)).Append('=')
                        .Append(reader.IsDBNull(column) ? "(null)" : Convert.ToString(reader.GetValue(column), CultureInfo.InvariantCulture));
                output.Append('\n');
            }
        }
        return output.ToString();
    }
}
