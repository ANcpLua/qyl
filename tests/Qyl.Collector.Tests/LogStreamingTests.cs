using System.Net;
using System.Text.Json;
using DuckDB.NET.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Collector.Hosting;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class LogStreamingTests
{
    [Fact]
    public async Task Empty_primary_key_drift_is_recreated_from_generated_schema()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"qyl-log-primary-key-{Guid.NewGuid():N}.duckdb");
        try
        {
            await using (var connection = new DuckDBConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                                      CREATE TABLE logs (
                                          project_id VARCHAR NOT NULL,
                                          log_id VARCHAR NOT NULL PRIMARY KEY
                                      );
                                      """;
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            await using (var store = new DuckDbStore(databasePath, maxConcurrentReads: 1))
            {
            }

            await using var verify = new DuckDBConnection($"Data Source={databasePath}");
            await verify.OpenAsync(TestContext.Current.CancellationToken);
            await using var primaryKey = verify.CreateCommand();
            primaryKey.CommandText = """
                                     SELECT unnest(constraint_column_names)
                                     FROM duckdb_constraints()
                                     WHERE table_name = 'logs'
                                       AND constraint_type = 'PRIMARY KEY'
                                     ORDER BY 1
                                     """;
            await using var reader = await primaryKey.ExecuteReaderAsync(
                TestContext.Current.CancellationToken);
            var actual = new List<string>();
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
                actual.Add(reader.GetString(0));

            Assert.Equal(["log_id", "project_id"], actual);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Later_ingested_older_event_is_delivered_after_the_live_cursor()
    {
        await using var store = new DuckDbStore(":memory:");
        await store.InsertLogsAsync(
            [CreateLog("newer-event", eventTime: 2_000_000_000)],
            TestContext.Current.CancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        await using var stream = CollectorEndpointExtensions.StreamLogEventsAsync(
                store,
                "default",
                serviceName: null,
                minSeverity: null,
                query: null,
                afterIngestSequence: null,
                timeout.Token)
            .GetAsyncEnumerator(timeout.Token);

        Assert.True(await stream.MoveNextAsync());
        var first = stream.Current;
        Assert.Equal("log", first.EventType);
        Assert.Equal<ulong>(2_000_000_000, Assert.IsType<Qyl.Api.Contracts.Streaming.LogStreamEvent>(first.Log).Data.TimeUnixNano);

        await store.InsertLogsAsync(
            [CreateLog("late-older-event", eventTime: 1_000_000_000)],
            timeout.Token);

        Assert.True(await stream.MoveNextAsync());
        var late = stream.Current;
        Assert.Equal("log", late.EventType);
        Assert.True(late.EventId > first.EventId);
        Assert.Equal<ulong>(1_000_000_000, Assert.IsType<Qyl.Api.Contracts.Streaming.LogStreamEvent>(late.Log).Data.TimeUnixNano);
    }

    [Fact]
    public async Task Saturated_live_stream_capacity_returns_the_generated_503_without_queueing()
    {
        var capacity = new CollectorStreamCapacity(maximum: 1);
        using var held = capacity.TryAcquire();
        Assert.NotNull(held);
        var context = CreateContext();

        await CollectorEndpointExtensions.StreamLogsAsync(
            context,
            store: null!,
            capacity,
            serviceName: null,
            query: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (HttpStatusCode)context.Response.StatusCode);
        Assert.Equal(ProblemDetailsMediaType.Value, context.Response.ContentType);
        context.Response.Body.Position = 0;
        var error = await JsonSerializer.DeserializeAsync(
            context.Response.Body,
            QylSerializerContext.Default.ServiceUnavailableError,
            TestContext.Current.CancellationToken);
        Assert.Equal(
            "collector.log_stream_capacity",
            Assert.IsType<ServiceUnavailableError>(error).Reason);
        Assert.Equal(1, capacity.Active);
    }

    private static LogStorageRow CreateLog(string id, ulong eventTime) =>
        new()
        {
            ProjectId = "default",
            LogId = id,
            TimeUnixNano = eventTime,
            SeverityNumber = 9,
            SeverityText = "info",
            Body = id,
            ServiceName = "stream-regression"
        };

    private static void DeleteDatabase(string databasePath)
    {
        File.Delete(databasePath);
        File.Delete($"{databasePath}.wal");
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();
        return context;
    }
}
