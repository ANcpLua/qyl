using System.Diagnostics;
using DuckDB.NET.Data;
using Qyl.Collector.Storage;
using Xunit;

namespace Qyl.Collector.Tests.Storage;

public sealed class DuckDbStoreRegressionTests
{
    [Fact]
    public async Task DetectRegressionsAsync_BatchesFingerprintLookup()
    {
        await using var store = new DuckDbStore(":memory:");
        const string serviceName = "test-service";
        const int count = 100;

        for (var index = 0; index < count; index++)
        {
            var fingerprint = $"fingerprint-{index}";
            await InsertErrorAsync(store, $"old-{index}", fingerprint, "resolved", serviceName);
            await InsertErrorAsync(store, $"new-{index}", fingerprint, "new", serviceName);
        }

        var sw = Stopwatch.StartNew();
        var regressedIds = await store.DetectRegressionsAsync(serviceName);
        sw.Stop();

        Assert.Equal(count, regressedIds.Count);
        Console.WriteLine($"DetectRegressionsAsync for {count} candidates took: {sw.ElapsedMilliseconds}ms");
    }

    private static Task InsertErrorAsync(
        DuckDbStore store,
        string errorId,
        string fingerprint,
        string status,
        string serviceName) =>
        store.ExecuteWriteAsync(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO errors (
                                  error_id,
                                  error_type,
                                  message,
                                  category,
                                  fingerprint,
                                  first_seen,
                                  last_seen,
                                  occurrence_count,
                                  status,
                                  affected_services
                              )
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = errorId });
            cmd.Parameters.Add(new DuckDBParameter { Value = "type" });
            cmd.Parameters.Add(new DuckDBParameter { Value = "msg" });
            cmd.Parameters.Add(new DuckDBParameter { Value = "cat" });
            cmd.Parameters.Add(new DuckDBParameter { Value = fingerprint });
            cmd.Parameters.Add(new DuckDBParameter { Value = DateTime.UtcNow });
            cmd.Parameters.Add(new DuckDBParameter { Value = DateTime.UtcNow });
            cmd.Parameters.Add(new DuckDBParameter { Value = 1L });
            cmd.Parameters.Add(new DuckDBParameter { Value = status });
            cmd.Parameters.Add(new DuckDBParameter { Value = serviceName });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });
}
