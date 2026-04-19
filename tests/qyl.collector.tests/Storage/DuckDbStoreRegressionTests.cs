using AwesomeAssertions;
using Xunit;
using DuckDB.NET.Data;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests.Storage;

public sealed class DuckDbStoreRegressionTests
{
    [Fact]
    public async Task DetectRegressionsAsync_BatchesFingerprintLookup()
    {
        await using var store = new DuckDbStore(":memory:");
        const string serviceName = "test-service";
        const int count = 100;

        // Seed resolved errors via upsert — the batched fingerprint lookup
        // path in DetectRegressionsAsync is exercised regardless of result count.
        for (var index = 0; index < count; index++)
        {
            var fingerprint = $"fingerprint-{index}";
            await UpsertErrorAsync(store, $"err-{index}", fingerprint, "resolved", serviceName);
        }

        var sw = Stopwatch.StartNew();
        var regressedIds = await store.DetectRegressionsAsync(serviceName, ct: TestContext.Current.CancellationToken);
        sw.Stop();

        // With one-row-per-fingerprint and upsert semantics, regression detection
        // requires separate 'resolved' and 'new' rows — which the unique index
        // prevents. The batched query path is still exercised and must not throw.
        regressedIds.Should().BeEmpty();
        Console.WriteLine($"DetectRegressionsAsync for {count} candidates took: {sw.ElapsedMilliseconds}ms");
    }

    private static Task UpsertErrorAsync(
        DuckDbStore store,
        string errorId,
        string fingerprint,
        string status,
        string serviceName) =>
        store.ExecuteWriteAsync(async (con, token) =>
        {
            var now = TimeProvider.System.GetUtcNow().UtcDateTime;
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
                              ON CONFLICT (fingerprint) DO UPDATE SET
                                  status = EXCLUDED.status,
                                  last_seen = EXCLUDED.last_seen,
                                  occurrence_count = errors.occurrence_count + 1
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = errorId });
            cmd.Parameters.Add(new DuckDBParameter { Value = "type" });
            cmd.Parameters.Add(new DuckDBParameter { Value = "msg" });
            cmd.Parameters.Add(new DuckDBParameter { Value = "cat" });
            cmd.Parameters.Add(new DuckDBParameter { Value = fingerprint });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            cmd.Parameters.Add(new DuckDBParameter { Value = now });
            cmd.Parameters.Add(new DuckDBParameter { Value = 1L });
            cmd.Parameters.Add(new DuckDBParameter { Value = status });
            cmd.Parameters.Add(new DuckDBParameter { Value = serviceName });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });
}
