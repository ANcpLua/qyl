using DuckDB.NET.Data;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class WorkflowStorageGeneratedPathTests
{
    [Fact]
    public async Task Generated_arrow_reader_streams_multiple_batches_and_observes_cancellation()
    {
        await using var connection = new DuckDBConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using (var create = connection.CreateCommand())
        {
            create.CommandText = WorkflowEventDbRow.CreateTableDdl;
            await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        using (var appender = WorkflowEventDbRow.CreateAppender(connection))
        {
            for (var index = 0; index < 5_000; index++)
            {
                WorkflowEventDbRow.AppendRow(appender, Event(index));
            }
        }

        await using var read = connection.CreateCommand();
        read.CommandText = "SELECT " + WorkflowEventDbRow.SelectColumnList +
                           " FROM workflow_events ORDER BY journal_sequence";
        var rows = new List<WorkflowEventDbRow>(5_000);
        var streamed = await WorkflowEventDbRow.ReadArrowRowsAsync(
            read,
            rows,
            static (target, row) => target.Add(row),
            TestContext.Current.CancellationToken);

        Assert.Equal(5_000, streamed.Rows);
        Assert.True(streamed.Batches > 1);
        Assert.Equal(5_000, rows.Count);
        Assert.Null(rows[0].AttemptId);
        Assert.Equal("attempt-4999", rows[^1].AttemptId);

        await using var cancelledRead = connection.CreateCommand();
        cancelledRead.CommandText = "SELECT " + WorkflowEventDbRow.SelectColumnList +
                                    " FROM workflow_events ORDER BY journal_sequence";
        using var cancellation = new CancellationTokenSource();
        var probe = new CancellationProbe(cancellation);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await WorkflowEventDbRow.ReadArrowRowsAsync(
                cancelledRead,
                probe,
                static (state, _) =>
                {
                    state.Rows++;
                    state.Cancellation.Cancel();
                },
                cancellation.Token));

        Assert.Equal(1, probe.Rows);
    }

    [Fact]
    public async Task Generated_arrow_reader_maps_empty_results_and_blob_bytes()
    {
        await using var connection = new DuckDBConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using (var create = connection.CreateCommand())
        {
            create.CommandText = WorkflowContentDbRow.CreateTableDdl;
            await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = WorkflowContentDbRow.BuildMultiRowInsertSql(1);
            WorkflowContentDbRow.AddParameters(insert, new WorkflowContentDbRow
            {
                ProjectId = "project-a",
                ContentRef = "sha256:" + new string('a', 64),
                ContentType = "application/octet-stream",
                Encoding = "base64",
                Nonce = [0, 1, 2],
                Tag = [3, 4, 5],
                Ciphertext = [6, 7, 8, 9],
                UncompressedSize = 4
            });
            await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var read = connection.CreateCommand();
        read.CommandText = "SELECT " + WorkflowContentDbRow.SelectColumnList +
                           " FROM workflow_content";
        var rows = new List<WorkflowContentDbRow>();
        var streamed = await WorkflowContentDbRow.ReadArrowRowsAsync(
            read,
            rows,
            static (target, row) => target.Add(row),
            TestContext.Current.CancellationToken);
        var row = Assert.Single(rows);
        Assert.Equal(1, streamed.Rows);
        Assert.Equal([0, 1, 2], row.Nonce);
        Assert.Equal([3, 4, 5], row.Tag);
        Assert.Equal([6, 7, 8, 9], row.Ciphertext);

        await using var empty = connection.CreateCommand();
        empty.CommandText = "SELECT " + WorkflowContentDbRow.SelectColumnList +
                            " FROM workflow_content WHERE false";
        var emptyRows = new List<WorkflowContentDbRow>();
        var emptyResult = await WorkflowContentDbRow.ReadArrowRowsAsync(
            empty,
            emptyRows,
            static (target, item) => target.Add(item),
            TestContext.Current.CancellationToken);
        Assert.Equal(0, emptyResult.Rows);
        Assert.Empty(emptyRows);
    }

    private static WorkflowEventDbRow Event(int index) => new()
    {
        ProjectId = "project-a",
        RunId = "run-1",
        JournalSequence = checked((ulong)index + 1),
        EventId = $"event-{index}",
        ClientId = "client-a",
        SourceSequence = checked((ulong)index + 1),
        EventTime = DateTimeOffset.UnixEpoch.AddMilliseconds(index),
        Kind = "content_captured",
        ThreadId = index % 2 is 0 ? null : "thread-a",
        TurnId = null,
        AttemptId = index is 4_999 ? "attempt-4999" : null,
        AgentId = null,
        ParentAgentId = null,
        ReceiverAgentId = null,
        ToolCallId = null,
        ContentRefsJson = "[]",
        DataJson = null
    };

    private sealed class CancellationProbe(CancellationTokenSource cancellation)
    {
        public CancellationTokenSource Cancellation { get; } = cancellation;

        public int Rows { get; set; }
    }
}
