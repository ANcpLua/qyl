using DuckDB.NET.Data;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Storage;
using Qyl.Collector.Storage.Generators;
using Qyl.Collector.Workflow;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Qyl.Collector.Tests;

public sealed class WorkflowStorageGeneratedPathTests
{
    [Fact]
    public async Task Generated_blob_appender_compiles_executes_and_round_trips_multiple_rows()
    {
        const string source = """
                              namespace Qyl.Collector.Storage;

                              [DuckDbTable("generated_blob_rows", AppenderEligible = true)]
                              internal sealed partial record GeneratedBlobRow
                              {
                                  public required byte[] Payload { get; init; }
                              }

                              internal static class DuckDbValueReader
                              {
                                  public static byte[] ReadBytes(
                                      System.Data.Common.DbDataReader reader,
                                      int ordinal) => reader.GetFieldValue<byte[]>(ordinal);
                              }
                              """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(typeof(DuckDBConnection).Assembly.Location)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            $"GeneratedBlobAppenderProbe_{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(
                source,
                parseOptions,
                cancellationToken: TestContext.Current.CancellationToken)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new DuckDbInsertGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var generatedCompilation,
            out var generatorDiagnostics,
            TestContext.Current.CancellationToken);
        Assert.Empty(generatorDiagnostics.Where(static diagnostic =>
            diagnostic.Severity is DiagnosticSeverity.Error));
        await using var assemblyBytes = new MemoryStream();
        var emit = generatedCompilation.Emit(
            assemblyBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(
            emit.Success,
            string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = Assembly.Load(assemblyBytes.ToArray());
        var rowType = assembly.GetType(
            "Qyl.Collector.Storage.GeneratedBlobRow",
            throwOnError: true)!;
        var createTableDdl = (string)rowType.GetField(
            "CreateTableDdl",
            BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue()!;
        var createAppender = rowType.GetMethod(
            "CreateAppender",
            BindingFlags.Public | BindingFlags.Static)!;
        var appendRow = rowType.GetMethod(
            "AppendRow",
            BindingFlags.Public | BindingFlags.Static)!;
        var payloadProperty = rowType.GetProperty("Payload")!;
        byte[][] expected =
        [
            [],
            [0, 1, 2, 3],
            Enumerable.Range(0, 257).Select(static value => (byte)(value % 256)).ToArray()
        ];

        await using var connection = new DuckDBConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using (var create = connection.CreateCommand())
        {
            create.CommandText = createTableDdl;
            await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        using (var appender = (IDisposable)createAppender.Invoke(null, [connection])!)
        {
            foreach (var payload in expected)
            {
                var row = Activator.CreateInstance(rowType)!;
                payloadProperty.SetValue(row, payload);
                appendRow.Invoke(null, [appender, row]);
            }
        }

        await using var read = connection.CreateCommand();
        read.CommandText = "SELECT payload FROM generated_blob_rows ORDER BY rowid";
        await using var reader = await read.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);
        var actual = new List<byte[]>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            actual.Add(DuckDbValueReader.ReadBytes(reader, 0));
        Assert.Equal(expected.Length, actual.Count);
        for (var index = 0; index < expected.Length; index++)
            Assert.Equal(expected[index], actual[index]);
    }

    [Fact]
    public async Task Failed_mid_batch_appender_rolls_back_journal_head_ack_and_manifest()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"qyl-generated-appender-rollback-{Guid.NewGuid():N}.duckdb");
        const string BlobPayload = "encrypted payload";
        var blobRef = ContentRef(BlobPayload);
        DuckDBAppender? failedAppender = null;
        var armed = 0;
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                beforeWorkflowEventAppend: (appender, index) =>
                {
                    if (Volatile.Read(ref armed) is 1 && index is 1)
                    {
                        failedAppender = appender;
                        appender.Dispose();
                    }
                });
            await store.CreateWorkflowRunAsync(
                new WorkflowRunStorageRow(
                    "project-a",
                    "run-1",
                    "thread-1",
                    "Appender rollback fixture",
                    WorkflowRunStatus.Active,
                    DateTimeOffset.UnixEpoch,
                    null,
                    0,
                    null,
                    null),
                TestContext.Current.CancellationToken);
            await store.AppendWorkflowEventsAsync(
                "project-a",
                "run-1",
                "client-a",
                [JournalEvent("one", 1)],
                [],
                TestContext.Current.CancellationToken);
            Assert.NotNull(await store.GetWorkflowGraphAsync(
                "project-a",
                "run-1",
                null,
                100,
                null,
                100,
                TestContext.Current.CancellationToken));
            var before = await store.GetWorkflowRunAsync(
                "project-a",
                "run-1",
                TestContext.Current.CancellationToken);
            Assert.NotNull(before);
            Volatile.Write(ref armed, 1);

            await Assert.ThrowsAnyAsync<Exception>(() =>
                store.AppendWorkflowEventsAsync(
                    "project-a",
                    "run-1",
                    "client-a",
                    [JournalEvent("two", 2, [blobRef]), JournalEvent("three", 3)],
                    [new WorkflowContentWrite(
                        blobRef,
                        "application/octet-stream",
                        WorkflowContentEncoding.Utf8,
                        BlobPayload)],
                    TestContext.Current.CancellationToken));
            Volatile.Write(ref armed, 0);

            Assert.NotNull(failedAppender);
            Assert.ThrowsAny<Exception>(() =>
                WorkflowEventDbRow.AppendRow(failedAppender, Event(99)));
            var after = await store.GetWorkflowRunAsync(
                "project-a",
                "run-1",
                TestContext.Current.CancellationToken);
            Assert.NotNull(after);
            Assert.Equal(before.LatestJournalSequence, after.LatestJournalSequence);
            Assert.Equal(before.EventCount, after.EventCount);
            Assert.Equal(before.ActiveCheckpointSequence, after.ActiveCheckpointSequence);
            Assert.Equal(before.ActiveCheckpointId, after.ActiveCheckpointId);
            Assert.Equal(before.ActiveCheckpointStorageKey, after.ActiveCheckpointStorageKey);
            Assert.Equal(before.ActiveCheckpointInputHash, after.ActiveCheckpointInputHash);
            Assert.Equal(before.CheckpointManifestEpoch, after.CheckpointManifestEpoch);
            var events = await store.ReadWorkflowEventsAsync(
                "project-a",
                "run-1",
                0,
                100,
                TestContext.Current.CancellationToken);
            Assert.Equal(["one"], events!.Events.Select(static item => item.EventId));

            await using (var inspection = new DuckDBConnection($"DataSource={databasePath}"))
            {
                await inspection.OpenAsync(TestContext.Current.CancellationToken);
                await using var ack = inspection.CreateCommand();
                ack.CommandText = """
                                  SELECT acknowledged_source_sequence
                                  FROM workflow_client_journal
                                  WHERE project_id = 'project-a'
                                    AND run_id = 'run-1'
                                    AND client_id = 'client-a'
                                  """;
                Assert.Equal(
                    1UL,
                    Convert.ToUInt64(
                        await ack.ExecuteScalarAsync(TestContext.Current.CancellationToken),
                        CultureInfo.InvariantCulture));
                await using var content = inspection.CreateCommand();
                content.CommandText = """
                                      SELECT count(*)
                                      FROM workflow_content
                                      WHERE project_id = 'project-a' AND content_ref = $1
                                      """;
                content.Parameters.Add(new DuckDBParameter { Value = blobRef });
                Assert.Equal(
                    0,
                    Convert.ToInt32(
                        await content.ExecuteScalarAsync(TestContext.Current.CancellationToken),
                        CultureInfo.InvariantCulture));
            }

            var retry = await store.AppendWorkflowEventsAsync(
                "project-a",
                "run-1",
                "client-a",
                [JournalEvent("two", 2, [blobRef]), JournalEvent("three", 3)],
                [new WorkflowContentWrite(
                    blobRef,
                    "application/octet-stream",
                    WorkflowContentEncoding.Utf8,
                    BlobPayload)],
                TestContext.Current.CancellationToken);
            Assert.Equal(2, retry.AcceptedCount);
            Assert.Equal(3UL, retry.AcknowledgedSourceSequence);
        }
        finally
        {
            File.Delete(databasePath);
            File.Delete($"{databasePath}.wal");
            var checkpoints = $"{databasePath}.workflow-checkpoints";
            if (Directory.Exists(checkpoints))
                Directory.Delete(checkpoints, recursive: true);
        }
    }

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

    private static WorkflowEventWrite JournalEvent(
        string eventId,
        ulong sourceSequence,
        IReadOnlyList<string>? contentRefs = null) =>
        new(
            eventId,
            sourceSequence,
            DateTimeOffset.UnixEpoch.AddMilliseconds(sourceSequence),
            WorkflowJournalEventKind.ContentCaptured,
            "thread-1",
            null,
            null,
            null,
            null,
            null,
            null,
            contentRefs ?? [],
            null);

    private static string ContentRef(string content) =>
        $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}";

    private sealed class CancellationProbe(CancellationTokenSource cancellation)
    {
        public CancellationTokenSource Cancellation { get; } = cancellation;

        public int Rows { get; set; }
    }
}
