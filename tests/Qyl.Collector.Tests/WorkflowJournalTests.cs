using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DuckDB.NET.Data;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class WorkflowJournalTests
{
    private static readonly DateTimeOffset s_startedAt =
        new(2026, 7, 28, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Append_is_idempotent_across_delayed_and_retried_batches()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);

        var delayed = await store.AppendWorkflowEventsAsync(
            "project-a",
            "run-1",
            "observer-1",
            [Event("event-3", 3, WorkflowJournalEventKind.ThreadStarted, milliseconds: 30)],
            [],
            TestContext.Current.CancellationToken);

        Assert.Equal(1, delayed.AcceptedCount);
        Assert.Equal(0UL, delayed.AcknowledgedSourceSequence);
        Assert.Equal(1UL, delayed.FirstJournalSequence);

        var gapFill = await store.AppendWorkflowEventsAsync(
            "project-a",
            "run-1",
            "observer-1",
            [
                Event("event-2", 2, WorkflowJournalEventKind.TurnStarted, milliseconds: 20, turnId: "turn-1"),
                Event("event-1", 1, WorkflowJournalEventKind.AttemptStarted, milliseconds: 10, attemptId: "attempt-1")
            ],
            [],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, gapFill.AcceptedCount);
        Assert.Equal(3UL, gapFill.AcknowledgedSourceSequence);
        Assert.Equal(2UL, gapFill.FirstJournalSequence);
        Assert.Equal(3UL, gapFill.LastJournalSequence);

        var retry = await store.AppendWorkflowEventsAsync(
            "project-a",
            "run-1",
            "observer-1",
            [
                Event("event-1", 1, WorkflowJournalEventKind.AttemptStarted, milliseconds: 10, attemptId: "attempt-1"),
                Event("event-2", 2, WorkflowJournalEventKind.TurnStarted, milliseconds: 20, turnId: "turn-1"),
                Event("event-3", 3, WorkflowJournalEventKind.ThreadStarted, milliseconds: 30)
            ],
            [],
            TestContext.Current.CancellationToken);

        Assert.Equal(0, retry.AcceptedCount);
        Assert.Equal(3, retry.DuplicateCount);
        Assert.Equal(3UL, retry.AcknowledgedSourceSequence);
        Assert.Null(retry.FirstJournalSequence);
        Assert.Null(retry.LastJournalSequence);

        var page = await store.ReadWorkflowEventsAsync(
            "project-a",
            "run-1",
            0,
            100,
            TestContext.Current.CancellationToken);
        Assert.NotNull(page);
        Assert.Equal(["event-3", "event-1", "event-2"], page.Events.Select(static item => item.EventId));
        Assert.Equal([1UL, 2UL, 3UL], page.Events.Select(static item => item.JournalSequence));
        Assert.Equal([3UL, 1UL, 2UL], page.Events.Select(static item => item.SourceSequence));

        await Assert.ThrowsAsync<WorkflowEventConflictException>(() =>
            store.AppendWorkflowEventsAsync(
                "project-a",
                "run-1",
                "observer-1",
                [Event("different-event", 1, WorkflowJournalEventKind.ThreadStarted)],
                [],
                TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<WorkflowEventConflictException>(() =>
            store.AppendWorkflowEventsAsync(
                "project-a",
                "run-1",
                "observer-1",
                [Event("event-1", 4, WorkflowJournalEventKind.ThreadStarted)],
                [],
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Replay_preserves_failed_attempt_before_resumed_success()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);

        await store.AppendWorkflowEventsAsync(
            "project-a",
            "run-1",
            "observer-1",
            [
                Event("a1-start", 1, WorkflowJournalEventKind.AttemptStarted, attemptId: "attempt-1"),
                Event("a1-turn", 2, WorkflowJournalEventKind.TurnStarted, 1, "attempt-1", turnId: "turn-1"),
                Event("a1-agent-spawn", 3, WorkflowJournalEventKind.AgentSpawned, 2, "attempt-1", "worker"),
                Event("a1-agent-start", 4, WorkflowJournalEventKind.AgentStarted, 3, "attempt-1", "worker"),
                Event("a1-agent-fail", 5, WorkflowJournalEventKind.AgentCompleted, 4, "attempt-1", "worker",
                    data: """{"status":"failed"}"""),
                Event("a1-turn-stop", 6, WorkflowJournalEventKind.TurnInterrupted, 5, "attempt-1",
                    turnId: "turn-1"),
                Event("a1-end", 7, WorkflowJournalEventKind.AttemptCompleted, 6, "attempt-1",
                    data: """{"status":"failed"}"""),
                Event("interrupted", 8, WorkflowJournalEventKind.TurnInterrupted, 7, "attempt-1",
                    turnId: "turn-1"),
                Event("a2-start", 9, WorkflowJournalEventKind.AttemptStarted, 8, "attempt-2"),
                Event("a2-turn", 10, WorkflowJournalEventKind.TurnStarted, 9, "attempt-2", turnId: "turn-2"),
                Event("a2-agent-spawn", 11, WorkflowJournalEventKind.AgentSpawned, 10, "attempt-2", "worker"),
                Event("a2-agent-start", 12, WorkflowJournalEventKind.AgentStarted, 11, "attempt-2", "worker"),
                Event("a2-agent-ok", 13, WorkflowJournalEventKind.AgentCompleted, 12, "attempt-2", "worker",
                    data: """{"status":"succeeded"}"""),
                Event("a2-turn-end", 14, WorkflowJournalEventKind.TurnCompleted, 13, "attempt-2",
                    turnId: "turn-2", data: """{"status":"succeeded"}"""),
                Event("a2-end", 15, WorkflowJournalEventKind.AttemptCompleted, 14, "attempt-2",
                    data: """{"status":"succeeded"}"""),
                Event("run-end", 16, WorkflowJournalEventKind.RunCompleted, 15,
                    data: """{"status":"completed"}""")
            ],
            [],
            TestContext.Current.CancellationToken);

        var before = await store.GetWorkflowGraphAsync(
            "project-a",
            "run-1",
            null,
            1000,
            null,
            2000,
            TestContext.Current.CancellationToken);
        Assert.NotNull(before);
        Assert.Equal(WorkflowRunStatus.Completed, before.Run.Status);
        Assert.Null(before.Run.ActiveAttemptId);
        Assert.Equal("failed", Assert.Single(before.Nodes, static node => node.NodeId == "attempt:attempt-1").Status);
        Assert.Equal("succeeded", Assert.Single(before.Nodes, static node => node.NodeId == "attempt:attempt-2").Status);
        Assert.Equal(
            "failed",
            Assert.Single(before.Nodes, static node => node.NodeId == "agent:attempt-1:worker").Status);
        Assert.Equal(
            "succeeded",
            Assert.Single(before.Nodes, static node => node.NodeId == "agent:attempt-2:worker").Status);
        Assert.Equal(
            "interrupted",
            Assert.Single(before.Nodes, static node => node.NodeId == "turn:attempt-1:turn-1").Status);

        var beforeJson = JsonSerializer.Serialize(before, QylSerializerContext.Default.WorkflowGraphSnapshot);
        await store.RebuildWorkflowProjectionAsync(
            "project-a",
            "run-1",
            TestContext.Current.CancellationToken);
        var after = await store.GetWorkflowGraphAsync(
            "project-a",
            "run-1",
            null,
            1000,
            null,
            2000,
            TestContext.Current.CancellationToken);
        var afterJson = JsonSerializer.Serialize(after, QylSerializerContext.Default.WorkflowGraphSnapshot);
        Assert.Equal(beforeJson, afterJson);
    }

    [Fact]
    public async Task Projection_distinguishes_recorded_edges_from_derived_conflicts_and_uses_timing()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);

        await store.AppendWorkflowEventsAsync(
            "project-a",
            "run-1",
            "observer-1",
            [
                Event("attempt", 1, WorkflowJournalEventKind.AttemptStarted, attemptId: "attempt-1"),
                Event("root-spawn", 2, WorkflowJournalEventKind.AgentSpawned, 0, "attempt-1", "root"),
                Event("root-start", 3, WorkflowJournalEventKind.AgentStarted, 0, "attempt-1", "root"),
                Event("w1-spawn", 4, WorkflowJournalEventKind.AgentSpawned, 1, "attempt-1", "worker-1",
                    parentAgentId: "root"),
                Event("w1-start", 5, WorkflowJournalEventKind.AgentStarted, 1, "attempt-1", "worker-1",
                    parentAgentId: "root"),
                Event("w2-spawn", 6, WorkflowJournalEventKind.AgentSpawned, 1, "attempt-1", "worker-2",
                    parentAgentId: "root"),
                Event("w2-start", 7, WorkflowJournalEventKind.AgentStarted, 1, "attempt-1", "worker-2",
                    parentAgentId: "root"),
                Event("w1-write", 8, WorkflowJournalEventKind.FileWritten, 2, "attempt-1", "worker-1",
                    data: """{"path":"src/shared.cs"}"""),
                Event("w2-write", 9, WorkflowJournalEventKind.FileWritten, 3, "attempt-1", "worker-2",
                    data: """{"path":"src/shared.cs"}"""),
                Event("message", 10, WorkflowJournalEventKind.MessageSent, 3, "attempt-1", "worker-1",
                    receiverAgentId: "worker-2"),
                Event("w2-end", 11, WorkflowJournalEventKind.AgentCompleted, 4, "attempt-1", "worker-2"),
                Event("w1-end", 12, WorkflowJournalEventKind.AgentCompleted, 5, "attempt-1", "worker-1"),
                Event("root-end", 13, WorkflowJournalEventKind.AgentCompleted, 6, "attempt-1", "root"),
                Event("attempt-end", 14, WorkflowJournalEventKind.AttemptCompleted, 6, "attempt-1")
            ],
            [],
            TestContext.Current.CancellationToken);

        var graph = await store.GetWorkflowGraphAsync(
            "project-a",
            "run-1",
            null,
            1000,
            null,
            2000,
            TestContext.Current.CancellationToken);
        Assert.NotNull(graph);

        var conflict = Assert.Single(graph.Edges, static edge => edge.Kind is WorkflowEdgeKind.Conflict);
        var derived = Assert.IsType<DerivedWorkflowEdgeProvenance>(conflict.Provenance);
        Assert.Equal(0.85, derived.Confidence);
        Assert.Equal(["w1-write", "w2-write"], derived.EventIds);

        var spawn = Assert.Single(
            graph.Edges,
            static edge => edge.SourceNodeId == "agent:attempt-1:root" &&
                           edge.TargetNodeId == "agent:attempt-1:worker-1" &&
                           edge.Kind is WorkflowEdgeKind.Control);
        Assert.IsType<RecordedWorkflowEdgeProvenance>(spawn.Provenance);
        Assert.Contains(
            graph.Edges,
            static edge => edge.SourceNodeId == "message:message" &&
                           edge.TargetNodeId == "agent:attempt-1:worker-2" &&
                           edge.Kind is WorkflowEdgeKind.Data);

        Assert.Equal(3, graph.Statistics.WorkerCount);
        Assert.Equal(3, graph.Statistics.PeakConcurrency);
        Assert.Equal(6, graph.Statistics.WallTimeMs);
        Assert.Equal(
            Math.Max(
                graph.Statistics.T1Ms / graph.Statistics.WorkerCount,
                graph.Statistics.TInfinityMs),
            graph.Statistics.ParallelLowerBoundMs);
    }

    [Fact]
    public async Task Captured_content_is_encrypted_lazy_and_project_scoped()
    {
        var databasePath = DatabasePath("content");
        var plaintext = $"workflow-secret-{Guid.NewGuid():N}-" + new string('z', 2048);
        var contentRef = ContentRef(plaintext);

        try
        {
            await using (var store = new DuckDbStore(databasePath, maxConcurrentReads: 1))
            {
                await CreateRunAsync(store);
                await store.AppendWorkflowEventsAsync(
                    "project-a",
                    "run-1",
                    "observer-1",
                    [Event("captured", 1, WorkflowJournalEventKind.ContentCaptured, contentRefs: [contentRef])],
                    [new WorkflowContentWrite(contentRef, "text/plain", WorkflowContentEncoding.Utf8, plaintext)],
                    TestContext.Current.CancellationToken);

                var content = await store.GetWorkflowContentAsync(
                    "project-a",
                    "run-1",
                    contentRef,
                    TestContext.Current.CancellationToken);
                Assert.NotNull(content);
                Assert.Equal(plaintext, content.Content);
                Assert.Equal(Encoding.UTF8.GetByteCount(plaintext), content.SizeBytes);
                Assert.Null(await store.GetWorkflowContentAsync(
                    "project-b",
                    "run-1",
                    contentRef,
                    TestContext.Current.CancellationToken));
                Assert.Null(await store.GetWorkflowContentAsync(
                    "project-a",
                    "another-run",
                    contentRef,
                    TestContext.Current.CancellationToken));

                var graph = await store.GetWorkflowGraphAsync(
                    "project-a",
                    "run-1",
                    null,
                    1000,
                    null,
                    2000,
                    TestContext.Current.CancellationToken);
                var graphJson = JsonSerializer.Serialize(
                    graph,
                    QylSerializerContext.Default.WorkflowGraphSnapshot);
                Assert.DoesNotContain(plaintext, graphJson, StringComparison.Ordinal);
            }

            var databaseBytes = await File.ReadAllBytesAsync(
                databasePath,
                TestContext.Current.CancellationToken);
            Assert.Equal(-1, databaseBytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(plaintext)));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Controls_are_idempotent_and_emit_each_durable_transition_once()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);

        var requested = await store.SubmitWorkflowControlAsync(
            "project-a",
            "run-1",
            WorkflowControlAction.Interrupt,
            "control-key",
            null,
            s_startedAt.AddSeconds(1),
            TestContext.Current.CancellationToken);
        Assert.NotNull(requested);

        var retried = await store.SubmitWorkflowControlAsync(
            "project-a",
            "run-1",
            WorkflowControlAction.Interrupt,
            "control-key",
            null,
            s_startedAt.AddSeconds(2),
            TestContext.Current.CancellationToken);
        Assert.Equal(requested.CommandId, retried!.CommandId);
        Assert.Equal(requested.RequestedAt, retried.RequestedAt);

        await Assert.ThrowsAsync<WorkflowControlConflictException>(() =>
            store.SubmitWorkflowControlAsync(
                "project-a",
                "run-1",
                WorkflowControlAction.Resume,
                "control-key",
                "continue",
                s_startedAt.AddSeconds(2),
                TestContext.Current.CancellationToken));

        var accepted = await store.UpdateWorkflowControlAsync(
            "project-a",
            "run-1",
            requested.CommandId,
            WorkflowControlStatus.Accepted,
            null,
            s_startedAt.AddSeconds(2),
            TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowControlStatus.Accepted, accepted!.Status);
        var applied = await store.UpdateWorkflowControlAsync(
            "project-a",
            "run-1",
            requested.CommandId,
            WorkflowControlStatus.Applied,
            null,
            s_startedAt.AddSeconds(3),
            TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowControlStatus.Applied, applied!.Status);
        var duplicateApplied = await store.UpdateWorkflowControlAsync(
            "project-a",
            "run-1",
            requested.CommandId,
            WorkflowControlStatus.Applied,
            null,
            s_startedAt.AddSeconds(4),
            TestContext.Current.CancellationToken);
        Assert.Equal(applied.UpdatedAt, duplicateApplied!.UpdatedAt);

        await Assert.ThrowsAsync<WorkflowControlConflictException>(() =>
            store.UpdateWorkflowControlAsync(
                "project-a",
                "run-1",
                requested.CommandId,
                WorkflowControlStatus.Rejected,
                "too late",
                s_startedAt.AddSeconds(5),
                TestContext.Current.CancellationToken));

        var commands = await store.PollWorkflowControlsAsync(
            "project-a",
            "run-1",
            0,
            10,
            TestContext.Current.CancellationToken);
        Assert.NotNull(commands);
        Assert.Equal(requested.CommandId, Assert.Single(commands.Commands).CommandId);

        var events = await store.ReadWorkflowEventsAsync(
            "project-a",
            "run-1",
            0,
            20,
            TestContext.Current.CancellationToken);
        Assert.NotNull(events);
        Assert.Equal(
            [
                WorkflowJournalEventKind.ControlRequested,
                WorkflowJournalEventKind.ControlAccepted,
                WorkflowJournalEventKind.ControlApplied
            ],
            events.Events.Select(static item => item.Kind));
    }

    [Fact]
    public async Task Retention_deletes_expired_runs_events_commands_and_unreferenced_content()
    {
        var databasePath = DatabasePath("retention");
        var plaintext = "expired workflow payload";
        var contentRef = ContentRef(plaintext);

        try
        {
            await using (var store = new DuckDbStore(databasePath, maxConcurrentReads: 1))
            {
                await CreateRunAsync(store, s_startedAt.AddDays(-45));
                await store.AppendWorkflowEventsAsync(
                    "project-a",
                    "run-1",
                    "observer-1",
                    [
                        Event("captured", 1, WorkflowJournalEventKind.ContentCaptured, contentRefs: [contentRef])
                            with { Timestamp = s_startedAt.AddDays(-45) },
                        Event("run-end", 2, WorkflowJournalEventKind.RunCompleted, 1,
                            data: """{"status":"completed"}""")
                            with { Timestamp = s_startedAt.AddDays(-45).AddMilliseconds(1) }
                    ],
                    [new WorkflowContentWrite(contentRef, "text/plain", WorkflowContentEncoding.Utf8, plaintext)],
                    TestContext.Current.CancellationToken);
                await store.SubmitWorkflowControlAsync(
                    "project-a",
                    "run-1",
                    WorkflowControlAction.Interrupt,
                    "expired-control",
                    null,
                    s_startedAt.AddDays(-45).AddSeconds(2),
                    TestContext.Current.CancellationToken);
            }

            await using (var connection = new DuckDBConnection($"DataSource={databasePath}"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                                      UPDATE workflow_content
                                      SET created_at = $1
                                      WHERE project_id = 'project-a' AND content_ref = $2
                                      """;
                command.Parameters.Add(new DuckDBParameter
                {
                    Value = s_startedAt.AddDays(-45).UtcDateTime
                });
                command.Parameters.Add(new DuckDBParameter { Value = contentRef });
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            await using (var store = new DuckDbStore(databasePath, maxConcurrentReads: 1))
            {
                var result = await store.DeleteExpiredWorkflowDataBatchAsync(
                    s_startedAt.AddDays(-30),
                    100,
                    TestContext.Current.CancellationToken);
                Assert.Equal(1, result.Runs);
                Assert.Equal(3, result.Events);
                Assert.Equal(1, result.Commands);
                Assert.Equal(1, result.Content);
                Assert.Null(await store.GetWorkflowRunAsync(
                    "project-a",
                    "run-1",
                    TestContext.Current.CancellationToken));
                Assert.Null(await store.GetWorkflowContentAsync(
                    "project-a",
                    "run-1",
                    contentRef,
                    TestContext.Current.CancellationToken));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Journal_pages_bound_large_histories()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);
        const int EventCount = 2_000;
        const int BatchSize = 500;

        for (var offset = 0; offset < EventCount; offset += BatchSize)
        {
            var events = Enumerable.Range(offset + 1, BatchSize)
                .Select(sequence => Event(
                    $"event-{sequence}",
                    (ulong)sequence,
                    WorkflowJournalEventKind.ContentCaptured,
                    sequence))
                .ToArray();
            await store.AppendWorkflowEventsAsync(
                "project-a",
                "run-1",
                "observer-1",
                events,
                [],
                TestContext.Current.CancellationToken);
        }

        var first = await store.ReadWorkflowEventsAsync(
            "project-a",
            "run-1",
            0,
            75,
            TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        Assert.Equal(75, first.Events.Count);
        Assert.Equal(75UL, first.NextSequence);
        Assert.Equal((ulong)EventCount, first.HighWaterMark);

        var tail = await store.ReadWorkflowEventsAsync(
            "project-a",
            "run-1",
            1_950,
            75,
            TestContext.Current.CancellationToken);
        Assert.NotNull(tail);
        Assert.Equal(50, tail.Events.Count);
        Assert.Equal((ulong)EventCount, tail.NextSequence);
    }

    [Fact]
    public async Task Large_graphs_return_independently_bounded_node_and_edge_windows()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);
        var events = new List<WorkflowEventWrite>
        {
            Event("attempt", 1, WorkflowJournalEventKind.AttemptStarted, attemptId: "attempt-1")
        };
        ulong sequence = 2;
        for (var index = 0; index < 300; index++)
        {
            var agent = $"worker-{index:D3}";
            events.Add(Event(
                $"agent-start-{index:D3}",
                sequence++,
                WorkflowJournalEventKind.AgentStarted,
                index,
                "attempt-1",
                agent));
            events.Add(Event(
                $"agent-end-{index:D3}",
                sequence++,
                WorkflowJournalEventKind.AgentCompleted,
                index + 1,
                "attempt-1",
                agent));
        }

        await store.AppendWorkflowEventsAsync(
            "project-a",
            "run-1",
            "client-a",
            events,
            [],
            TestContext.Current.CancellationToken);

        var first = await store.GetWorkflowGraphAsync(
            "project-a",
            "run-1",
            null,
            25,
            null,
            30,
            TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        Assert.Equal(25, first.Nodes.Count);
        Assert.True(first.Edges.Count <= 30);
        Assert.True(first.HasMoreNodes);
        Assert.True(first.HasMoreEdges);
        // Keyset, not offset: the cursor is the last id on the page, so a projection rebuilt
        // between pages cannot shift rows out from under it.
        Assert.Equal(first.Nodes[^1].NodeId, first.NextNodeCursor);
        Assert.Equal(first.Edges[^1].EdgeId, first.NextEdgeCursor);
        Assert.True(first.TotalNodeCount > first.Nodes.Count);
        Assert.True(first.TotalEdgeCount > first.Edges.Count);

        var second = await store.GetWorkflowGraphAsync(
            "project-a",
            "run-1",
            first.NextNodeCursor,
            25,
            first.NextEdgeCursor,
            30,
            TestContext.Current.CancellationToken);
        Assert.NotNull(second);
        Assert.Empty(first.Nodes.Select(static node => node.NodeId)
            .Intersect(second.Nodes.Select(static node => node.NodeId), StringComparer.Ordinal));
        Assert.Equal(first.JournalSequence, second.JournalSequence);
        Assert.Equal(first.Statistics.T1Ms, second.Statistics.T1Ms);
    }

    private static Task<WorkflowRunStorageRow> CreateRunAsync(
        DuckDbStore store,
        DateTimeOffset? startedAt = null) =>
        store.CreateWorkflowRunAsync(
            new WorkflowRunStorageRow(
                "project-a",
                "run-1",
                "thread-1",
                "Observe Graph fixture",
                WorkflowRunStatus.Active,
                startedAt ?? s_startedAt,
                null,
                0,
                null,
                null),
            TestContext.Current.CancellationToken);

    private static WorkflowEventWrite Event(
        string eventId,
        ulong sourceSequence,
        WorkflowJournalEventKind kind,
        int milliseconds = 0,
        string? attemptId = null,
        string? agentId = null,
        string? parentAgentId = null,
        string? receiverAgentId = null,
        string? turnId = null,
        string? toolCallId = null,
        IReadOnlyList<string>? contentRefs = null,
        string? data = null) =>
        new(
            eventId,
            sourceSequence,
            s_startedAt.AddMilliseconds(milliseconds),
            kind,
            "thread-1",
            turnId,
            attemptId,
            agentId,
            parentAgentId,
            receiverAgentId,
            toolCallId,
            contentRefs ?? [],
            data);

    private static string ContentRef(string plaintext) =>
        $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)))}";

    private static string DatabasePath(string testName) =>
        Path.Combine(Path.GetTempPath(), $"qyl-workflow-{testName}-{Guid.NewGuid():N}.duckdb");

    private static void DeleteDatabase(string databasePath)
    {
        File.Delete(databasePath);
        File.Delete($"{databasePath}.wal");
    }
}
