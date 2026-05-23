using DuckDB.NET.Data;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests.Storage;

public sealed class DuckDbStoreServiceAggregationTests
{
    [Fact]
    public async Task UpdateServiceAggregatesAsync_CountsNumericErrorStatuses_AndIgnoresNonnumericStatusText()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = new DuckDbStore(":memory:");
        var serviceName = $"aggregate-status-{Guid.NewGuid():N}";
        var observedAt = new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.Zero);

        await store.UpsertServiceInstanceAsync(new ServiceInstanceRecord
        {
            ServiceNamespace = "",
            ServiceName = serviceName,
            ServiceInstanceId = "test-instance",
            ServiceType = "traditional",
            TimestampNano = ToUnixNanoseconds(observedAt)
        }, ct);

        await SeedSpanAsync(store, $"error-{serviceName}", serviceName, observedAt, "2", ct);
        await SeedSpanAsync(store, $"text-{serviceName}", serviceName, observedAt.AddSeconds(1), "ERROR", ct);

        await store.UpdateServiceAggregatesAsync(ct);

        var services = await store.GetServicesAsync(limit: 10, ct: ct);
        var service = services.Should().ContainSingle(item => item.ServiceName == serviceName).Subject;
        service.TotalSpans.Should().Be(2);
        service.TotalErrors.Should().Be(1);
    }

    private static async Task SeedSpanAsync(
        DuckDbStore store,
        string spanId,
        string serviceName,
        DateTimeOffset start,
        string statusCode,
        CancellationToken ct)
    {
        var startNano = ToUnixNanoseconds(start);

        await store.ExecuteWriteAsync(async (connection, token) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                              INSERT INTO spans
                                  (span_id, trace_id, name, kind, start_time_unix_nano, end_time_unix_nano,
                                   duration_ns, status_code, service_name)
                              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
                              """;
            cmd.Parameters.Add(new DuckDBParameter { Value = spanId });
            cmd.Parameters.Add(new DuckDBParameter { Value = $"trace-{spanId}" });
            cmd.Parameters.Add(new DuckDBParameter { Value = "operation" });
            cmd.Parameters.Add(new DuckDBParameter { Value = "internal" });
            cmd.Parameters.Add(new DuckDBParameter { Value = startNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = startNano + 1_000_000L });
            cmd.Parameters.Add(new DuckDBParameter { Value = 1_000_000L });
            cmd.Parameters.Add(new DuckDBParameter { Value = statusCode });
            cmd.Parameters.Add(new DuckDBParameter { Value = serviceName });
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    private static ulong ToUnixNanoseconds(DateTimeOffset value)
    {
        var seconds = value.ToUnixTimeSeconds();
        var ticksWithinSecond = value.Ticks % TimeSpan.TicksPerSecond;
        return (ulong)((seconds * 1_000_000_000L) + (ticksWithinSecond * 100L));
    }
}
