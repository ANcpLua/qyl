using System.Diagnostics;
using qyl.collector.Storage;
using qyl.collector.Errors;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace qyl.collector.tests.Storage;

public sealed class DuckDbStoreRegressionTests
{
    [Fact]
    public async Task DetectRegressionsAsync_PerformanceBaseline()
    {
        await using var store = new DuckDbStore(":memory:");
        var serviceName = "test-service";
        int count = 100; // Number of resolved errors to check

        // 1. Setup: Insert resolved errors and matching new errors
        for (int i = 0; i < count; i++)
        {
            var fingerprint = $"fingerprint-{i}";

            // Insert resolved error
            await store.ExecuteWriteAsync(async (con, token) => {
                await using var cmd = con.CreateCommand();
                cmd.CommandText = "INSERT INTO errors (error_id, error_type, message, category, fingerprint, first_seen, last_seen, occurrence_count, status, affected_services) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)";
                cmd.Parameters.Add(new DuckDBParameter { Value = $"old-{i}" });
                cmd.Parameters.Add(new DuckDBParameter { Value = "type" });
                cmd.Parameters.Add(new DuckDBParameter { Value = "msg" });
                cmd.Parameters.Add(new DuckDBParameter { Value = "cat" });
                cmd.Parameters.Add(new DuckDBParameter { Value = fingerprint });
                cmd.Parameters.Add(new DuckDBParameter { Value = DateTime.UtcNow });
                cmd.Parameters.Add(new DuckDBParameter { Value = DateTime.UtcNow });
                cmd.Parameters.Add(new DuckDBParameter { Value = 1L });
                cmd.Parameters.Add(new DuckDBParameter { Value = "resolved" });
                cmd.Parameters.Add(new DuckDBParameter { Value = serviceName });
                await cmd.ExecuteNonQueryAsync(token);
                return 0;
            });

            // Insert new error with same fingerprint
            await store.ExecuteWriteAsync(async (con, token) => {
                await using var cmd = con.CreateCommand();
                cmd.CommandText = "INSERT INTO errors (error_id, error_type, message, category, fingerprint, first_seen, last_seen, occurrence_count, status, affected_services) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)";
                cmd.Parameters.Add(new DuckDBParameter { Value = $"new-{i}" });
                cmd.Parameters.Add(new DuckDBParameter { Value = "type" });
                cmd.Parameters.Add(new DuckDBParameter { Value = "msg" });
                cmd.Parameters.Add(new DuckDBParameter { Value = "cat" });
                cmd.Parameters.Add(new DuckDBParameter { Value = fingerprint });
                cmd.Parameters.Add(new DuckDBParameter { Value = DateTime.UtcNow });
                cmd.Parameters.Add(new DuckDBParameter { Value = DateTime.UtcNow });
                cmd.Parameters.Add(new DuckDBParameter { Value = 1L });
                cmd.Parameters.Add(new DuckDBParameter { Value = "new" });
                cmd.Parameters.Add(new DuckDBParameter { Value = serviceName });
                await cmd.ExecuteNonQueryAsync(token);
                return 0;
            });
        }

        // 2. Measure
        var sw = Stopwatch.StartNew();
        var regressedIds = await store.DetectRegressionsAsync(serviceName);
        sw.Stop();

        Assert.Equal(count, regressedIds.Count);
        Console.WriteLine($"DetectRegressionsAsync for {count} candidates took: {sw.ElapsedMilliseconds}ms");
    }
}
