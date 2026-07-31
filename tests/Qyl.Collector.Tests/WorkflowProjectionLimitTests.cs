using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DuckDB.NET.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Api.Contracts.Common.Errors;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Hosting;
using Qyl.Collector.Storage;
using Qyl.Collector.Workflow;

namespace Qyl.Collector.Tests;

public sealed class WorkflowProjectionLimitTests
{
    private static readonly DateTimeOffset s_timestamp =
        new(2026, 7, 29, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Duplicate_only_batches_succeed_at_capacity_without_rebuilding()
    {
        var databasePath = DatabasePath("duplicate-cap");
        var events = new[]
        {
            Event("event-1", 1),
            Event("event-2", 2)
        };
        try
        {
            await using (var seed = new DuckDbStore(
                             databasePath,
                             maxConcurrentReads: 1,
                             workflowProjectionLimits: new WorkflowProjectionLimits(maxEventsPerRun: 2)))
            {
                await CreateRunAsync(seed);
                await seed.AppendWorkflowEventsAsync(
                    "project-a", "run-1", "client-a", events, [],
                    TestContext.Current.CancellationToken);
            }

            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(
                    maxEventsPerRun: 2,
                    maxWorkUnits: 0));
            var retry = await store.AppendWorkflowEventsAsync(
                "project-a", "run-1", "client-a", events, [],
                TestContext.Current.CancellationToken);

            Assert.Equal(0, retry.AcceptedCount);
            Assert.Equal(2, retry.DuplicateCount);

            var invalidContent = new WorkflowContentWrite(
                $"sha256:{new string('a', 64)}",
                "application/octet-stream",
                WorkflowContentEncoding.Base64,
                "not base64 or secret-safe");
            await Assert.ThrowsAsync<WorkflowProjectionLimitExceededException>(() =>
                store.AppendWorkflowEventsAsync(
                    "project-a",
                    "run-1",
                    "client-a",
                    [events[0], Event("event-3", 3)],
                    [invalidContent],
                    TestContext.Current.CancellationToken));

            var page = await store.ReadWorkflowEventsAsync(
                "project-a", "run-1", 0, 10, TestContext.Current.CancellationToken);
            Assert.Equal(["event-1", "event-2"], page!.Events.Select(static item => item.EventId));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Projection_node_and_edge_failures_preserve_the_accepted_journal()
    {
        await AssertProjectionFailureAsync(
            new WorkflowProjectionLimits(maxNodes: 2),
            [
                Event("attempt", 1, WorkflowJournalEventKind.AttemptStarted, attemptId: "attempt-1"),
                Event("agent", 2, WorkflowJournalEventKind.AgentStarted, attemptId: "attempt-1", agentId: "agent")
            ]);

        await AssertProjectionFailureAsync(
            new WorkflowProjectionLimits(maxEdges: 1),
            [
                Event("attempt", 1, WorkflowJournalEventKind.AttemptStarted, attemptId: "attempt-1"),
                Event("parent", 2, WorkflowJournalEventKind.AgentStarted, attemptId: "attempt-1", agentId: "parent"),
                Event("child", 3, WorkflowJournalEventKind.AgentStarted, attemptId: "attempt-1", agentId: "child",
                    parentAgentId: "parent")
            ]);
    }

    [Fact]
    public async Task Empty_projection_consumes_exact_work_floor_and_the_next_event_exceeds_it()
    {
        await using (var belowFloor = new DuckDbStore(
                         ":memory:",
                         workflowProjectionLimits: new WorkflowProjectionLimits(maxWorkUnits: 2)))
        {
            await CreateRunAsync(belowFloor);
            await Assert.ThrowsAsync<WorkflowProjectionLimitExceededException>(() =>
                belowFloor.GetWorkflowGraphAsync(
                    "project-a", "run-1", null, 100, null, 100,
                    TestContext.Current.CancellationToken));
        }

        await using var store = new DuckDbStore(
            ":memory:",
            workflowProjectionLimits: new WorkflowProjectionLimits(maxWorkUnits: 3));
        await CreateRunAsync(store);
        var empty = await store.GetWorkflowGraphAsync(
            "project-a", "run-1", null, 100, null, 100,
            TestContext.Current.CancellationToken);
        Assert.NotNull(empty);
        Assert.Equal(0UL, empty.JournalSequence);

        var append = await store.AppendWorkflowEventsAsync(
            "project-a",
            "run-1",
            "client-a",
            [Event("work", 1)],
            [],
            TestContext.Current.CancellationToken);
        Assert.Equal(1, append.AcceptedCount);
        await Assert.ThrowsAsync<WorkflowProjectionLimitExceededException>(() =>
            store.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken));
        Assert.Single((await store.ReadWorkflowEventsAsync(
            "project-a", "run-1", 0, 10, TestContext.Current.CancellationToken))!.Events);
    }

    [Fact]
    public async Task Serialized_input_limits_reject_before_any_journal_mutation()
    {
        await using var store = new DuckDbStore(
            ":memory:",
            workflowProjectionLimits: new WorkflowProjectionLimits(maxSerializedInputBytes: 512));
        await CreateRunAsync(store);
        await Assert.ThrowsAsync<WorkflowProjectionLimitExceededException>(() =>
            store.AppendWorkflowEventsAsync(
                "project-a",
                "run-1",
                "client-a",
                [Event("large", 1) with { DataJson = $"{{\"value\":\"{new string('x', 1024)}\"}}" }],
                [],
                TestContext.Current.CancellationToken));
        var run = await store.GetWorkflowRunAsync(
            "project-a", "run-1", TestContext.Current.CancellationToken);
        Assert.Equal(0UL, run!.LatestJournalSequence);
        Assert.Empty((await store.ReadWorkflowEventsAsync(
            "project-a", "run-1", 0, 10, TestContext.Current.CancellationToken))!.Events);
    }

    [Fact]
    public async Task Oversized_run_metadata_rejects_before_creating_a_run_or_checkpoint()
    {
        await using var store = new DuckDbStore(
            ":memory:",
            workflowProjectionLimits: new WorkflowProjectionLimits(maxSerializedInputBytes: 256));
        await Assert.ThrowsAsync<WorkflowProjectionLimitExceededException>(() =>
            store.CreateWorkflowRunAsync(
                new WorkflowRunStorageRow(
                    "project-a",
                    "run-oversized",
                    "thread-1",
                    "Oversized",
                    WorkflowRunStatus.Active,
                    s_timestamp,
                    null,
                    0,
                    null,
                    $"{{\"value\":\"{new string('x', 512)}\"}}"),
                TestContext.Current.CancellationToken));
        Assert.Null(await store.GetWorkflowRunAsync(
            "project-a", "run-oversized", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rebuild_rejects_a_legacy_journal_beyond_the_current_event_limit()
    {
        var databasePath = DatabasePath("legacy-limit");
        try
        {
            await using (var seed = new DuckDbStore(databasePath, maxConcurrentReads: 1))
            {
                await CreateRunAsync(seed);
                await seed.AppendWorkflowEventsAsync(
                    "project-a", "run-1", "client-a",
                    [Event("one", 1), Event("two", 2), Event("three", 3)],
                    [], TestContext.Current.CancellationToken);
            }

            await using var constrained = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(maxEventsPerRun: 2));
            await Assert.ThrowsAsync<WorkflowProjectionLimitExceededException>(() =>
                constrained.RebuildWorkflowProjectionAsync(
                    "project-a", "run-1", TestContext.Current.CancellationToken));
            var page = await constrained.ReadWorkflowEventsAsync(
                "project-a", "run-1", 0, 10, TestContext.Current.CancellationToken);
            Assert.Equal(3, page!.Events.Count);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Control_event_capacity_is_checked_before_command_mutation()
    {
        await using var store = new DuckDbStore(
            ":memory:",
            workflowProjectionLimits: new WorkflowProjectionLimits(maxEventsPerRun: 2));
        await CreateRunAsync(store);
        var command = await store.SubmitWorkflowControlAsync(
            "project-a",
            "run-1",
            WorkflowControlAction.Interrupt,
            "command-one",
            null,
            s_timestamp,
            TestContext.Current.CancellationToken);
        Assert.NotNull(command);
        await store.AppendWorkflowEventsAsync(
            "project-a", "run-1", "client-a", [Event("second-event", 1)], [],
            TestContext.Current.CancellationToken);
        var beforeRejection = await store.GetWorkflowRunAsync(
            "project-a", "run-1", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<WorkflowProjectionLimitExceededException>(() =>
            store.SubmitWorkflowControlAsync(
                "project-a",
                "run-1",
                WorkflowControlAction.Interrupt,
                "command-two",
                null,
                s_timestamp,
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<WorkflowProjectionLimitExceededException>(() =>
            store.UpdateWorkflowControlAsync(
                "project-a",
                "run-1",
                command!.CommandId,
                WorkflowControlStatus.Accepted,
                null,
                s_timestamp.AddSeconds(1),
                TestContext.Current.CancellationToken));

        var commands = await store.PollWorkflowControlsAsync(
            "project-a", "run-1", 0, 10, TestContext.Current.CancellationToken);
        var persisted = Assert.Single(commands!.Commands);
        Assert.Equal(WorkflowControlStatus.Requested, persisted.Status);
        var events = await store.ReadWorkflowEventsAsync(
            "project-a", "run-1", 0, 10, TestContext.Current.CancellationToken);
        Assert.Equal(2, events!.Events.Count);
        var afterRejection = await store.GetWorkflowRunAsync(
            "project-a", "run-1", TestContext.Current.CancellationToken);
        Assert.Equal(beforeRejection!.NextCommandSequence, afterRejection!.NextCommandSequence);
        Assert.Equal(
            beforeRejection.NextControlEventSourceSequence,
            afterRejection.NextControlEventSourceSequence);
    }

    [Fact]
    public async Task Checkpoint_write_stream_rejects_before_forwarding_or_allocating_past_the_ceiling()
    {
        const int MaximumBytes = 32;
        using var destination = new WorkflowCheckpointMemoryStream(MaximumBytes);
        using var stream = new WorkflowCheckpointWriteStream(destination, MaximumBytes);
        await stream.WriteAsync(
            new byte[MaximumBytes],
            TestContext.Current.CancellationToken);
        var allocatedBeforeRejection = destination.AllocatedBytes;

        await Assert.ThrowsAsync<WorkflowProjectionLimitExceededException>(() =>
            stream.WriteAsync(
                    new byte[1],
                    TestContext.Current.CancellationToken)
                .AsTask());

        Assert.Equal((long)MaximumBytes, stream.BytesWritten);
        Assert.Equal((long)MaximumBytes, destination.Length);
        Assert.Equal(allocatedBeforeRejection, destination.AllocatedBytes);
        Assert.InRange(destination.AllocatedBytes, 1, MaximumBytes);
    }

    [Fact]
    public async Task Oversized_checkpoint_serialization_preserves_the_committed_journal_without_a_blob()
    {
        var databasePath = DatabasePath("checkpoint-write-cap");
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(maxCheckpointBytes: 1024));
            await store.CreateWorkflowRunAsync(
                new WorkflowRunStorageRow(
                    "project-a",
                    "run-oversized-checkpoint",
                    "thread-1",
                    new string('x', 4096),
                    WorkflowRunStatus.Active,
                    s_timestamp,
                    null,
                    0,
                    null,
                    null),
                TestContext.Current.CancellationToken);
            await store.AppendWorkflowEventsAsync(
                "project-a",
                "run-oversized-checkpoint",
                "client-a",
                [Event("one", 1)],
                [],
                TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<WorkflowProjectionLimitExceededException>(() =>
                store.GetWorkflowGraphAsync(
                    "project-a",
                    "run-oversized-checkpoint",
                    null,
                    100,
                    null,
                    100,
                    TestContext.Current.CancellationToken));

            var persisted = await store.GetWorkflowRunAsync(
                "project-a",
                "run-oversized-checkpoint",
                TestContext.Current.CancellationToken);
            Assert.Equal(1UL, persisted!.LatestJournalSequence);
            var checkpointRoot = $"{databasePath}.workflow-checkpoints";
            if (Directory.Exists(checkpointRoot))
            {
                Assert.Empty(Directory.GetFiles(
                    checkpointRoot,
                    "*",
                    SearchOption.AllDirectories));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Oversized_checkpoint_is_rejected_by_length_and_rebuilt_within_the_ceiling()
    {
        var databasePath = DatabasePath("checkpoint-length");
        const int MaxCheckpointBytes = 64 * 1024;
        try
        {
            await using (var seed = new DuckDbStore(databasePath, maxConcurrentReads: 1))
            {
                await CreateRunAsync(seed);
                await seed.AppendWorkflowEventsAsync(
                    "project-a",
                    "run-1",
                    "client-a",
                    [Event("one", 1), Event("two", 2)],
                    [],
                    TestContext.Current.CancellationToken);
                Assert.NotNull(await seed.GetWorkflowGraphAsync(
                    "project-a", "run-1", null, 100, null, 100,
                    TestContext.Current.CancellationToken));
            }

            var checkpoint = Assert.Single(Directory.GetFiles(
                $"{databasePath}.workflow-checkpoints",
                "*.json",
                SearchOption.AllDirectories));
            await using (var stream = new FileStream(
                             checkpoint,
                             FileMode.Open,
                             FileAccess.Write,
                             FileShare.None))
            {
                stream.SetLength(MaxCheckpointBytes + 1L);
            }

            await using var recovered = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(
                    maxCheckpointBytes: MaxCheckpointBytes));
            var graph = await recovered.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken);

            Assert.NotNull(graph);
            Assert.Equal(2UL, graph.JournalSequence);
            // The superseded blob is removed by the sweep, not by publication.
            while (await recovered.ReconcileWorkflowCheckpointsAsync(
                       TestContext.Current.CancellationToken))
            {
            }
            var boundedCheckpoint = Assert.Single(Directory.GetFiles(
                $"{databasePath}.workflow-checkpoints",
                "*.json",
                SearchOption.AllDirectories));
            Assert.InRange(
                new FileInfo(boundedCheckpoint).Length,
                1L,
                (long)MaxCheckpointBytes);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Recovery_limit_failure_is_durable_until_head_or_configuration_changes()
    {
        var databasePath = DatabasePath("durable-recovery-limit");
        try
        {
            WorkflowRunStorageRow original;
            await using (var seed = new DuckDbStore(databasePath, maxConcurrentReads: 1))
            {
                await CreateRunAsync(seed);
                await seed.AppendWorkflowEventsAsync(
                    "project-a",
                    "run-1",
                    "client-a",
                    [
                        Event("attempt", 1, WorkflowJournalEventKind.AttemptStarted,
                            attemptId: "attempt-1"),
                        Event("agent", 2, WorkflowJournalEventKind.AgentStarted,
                            attemptId: "attempt-1", agentId: "agent-1")
                    ],
                    [],
                    TestContext.Current.CancellationToken);
                Assert.NotNull(await seed.GetWorkflowGraphAsync(
                    "project-a", "run-1", null, 100, null, 100,
                    TestContext.Current.CancellationToken));
                original = (await seed.GetWorkflowRunAsync(
                    "project-a", "run-1", TestContext.Current.CancellationToken))!;
            }

            var checkpoint = Assert.Single(Directory.GetFiles(
                $"{databasePath}.workflow-checkpoints",
                "*.json",
                SearchOption.AllDirectories));
            await File.WriteAllTextAsync(
                checkpoint,
                """{"corrupt":true}""",
                TestContext.Current.CancellationToken);

            var writes = 0;
            var limits = new WorkflowProjectionLimits(maxNodes: 2);
            await using (var recovered = new DuckDbStore(
                             databasePath,
                             maxConcurrentReads: 1,
                             beforeWrite: _ =>
                             {
                                 Interlocked.Increment(ref writes);
                                 return ValueTask.CompletedTask;
                             },
                             workflowProjectionLimits: limits))
            {
                await Assert.ThrowsAsync<WorkflowProjectionLimitExceededException>(() =>
                    recovered.GetWorkflowGraphAsync(
                        "project-a", "run-1", null, 100, null, 100,
                        TestContext.Current.CancellationToken));

                var failed = await recovered.GetWorkflowRunAsync(
                    "project-a", "run-1", TestContext.Current.CancellationToken);
                Assert.Equal(original.ActiveCheckpointId, failed!.ActiveCheckpointId);
                Assert.Equal(original.ActiveCheckpointSequence, failed.ActiveCheckpointSequence);
                Assert.Equal(failed.LatestJournalSequence, failed.ProjectionFailureSequence!.Value);
                Assert.Equal("limit", failed.ProjectionFailureKind);
                Assert.Equal(limits.ConfigurationFingerprint, failed.ProjectionFailureConfiguration);
                Assert.Equal(
                    WorkflowProjectionBuilder.SemanticFingerprint,
                    failed.ProjectionFailureSemantic);
                var writesAfterFirstFailure = Volatile.Read(ref writes);

                await Assert.ThrowsAsync<WorkflowProjectionLimitExceededException>(() =>
                    recovered.GetWorkflowGraphAsync(
                        "project-a", "run-1", null, 100, null, 100,
                        TestContext.Current.CancellationToken));
                Assert.Equal(writesAfterFirstFailure, Volatile.Read(ref writes));
            }

            await using var relaxed = new DuckDbStore(databasePath, maxConcurrentReads: 1);
            var rebuilt = await relaxed.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken);
            var recoveredRun = await relaxed.GetWorkflowRunAsync(
                "project-a", "run-1", TestContext.Current.CancellationToken);
            Assert.Equal(2UL, rebuilt!.JournalSequence);
            Assert.Null(recoveredRun!.ProjectionFailureSequence);
            Assert.Null(recoveredRun.ProjectionFailureKind);
            Assert.Null(recoveredRun.ProjectionFailureConfiguration);
            Assert.Null(recoveredRun.ProjectionFailureSemantic);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Recovery_invalid_failure_is_durable_until_configuration_changes()
    {
        var databasePath = DatabasePath("durable-recovery-invalid");
        try
        {
            WorkflowRunStorageRow original;
            await using (var seed = new DuckDbStore(databasePath, maxConcurrentReads: 1))
            {
                await CreateRunAsync(seed);
                await seed.AppendWorkflowEventsAsync(
                    "project-a",
                    "run-1",
                    "client-a",
                    [Event("one", 1)],
                    [],
                    TestContext.Current.CancellationToken);
                Assert.NotNull(await seed.GetWorkflowGraphAsync(
                    "project-a", "run-1", null, 100, null, 100,
                    TestContext.Current.CancellationToken));
                original = (await seed.GetWorkflowRunAsync(
                    "project-a", "run-1", TestContext.Current.CancellationToken))!;
            }

            var checkpoint = Assert.Single(Directory.GetFiles(
                $"{databasePath}.workflow-checkpoints",
                "*.json",
                SearchOption.AllDirectories));
            await File.WriteAllTextAsync(
                checkpoint,
                """{"corrupt":true}""",
                TestContext.Current.CancellationToken);
            await SetProjectionInputBytesAsync(
                databasePath,
                original.ProjectionInputBytes + 1);

            var writes = 0;
            var limits = new WorkflowProjectionLimits();
            await using (var recovered = new DuckDbStore(
                             databasePath,
                             maxConcurrentReads: 1,
                             beforeWrite: _ =>
                             {
                                 Interlocked.Increment(ref writes);
                                 return ValueTask.CompletedTask;
                             },
                             workflowProjectionLimits: limits))
            {
                await Assert.ThrowsAsync<InvalidDataException>(() =>
                    recovered.GetWorkflowGraphAsync(
                        "project-a", "run-1", null, 100, null, 100,
                        TestContext.Current.CancellationToken));

                var failed = await recovered.GetWorkflowRunAsync(
                    "project-a", "run-1", TestContext.Current.CancellationToken);
                Assert.Equal(original.ActiveCheckpointId, failed!.ActiveCheckpointId);
                Assert.Equal(original.ActiveCheckpointSequence, failed.ActiveCheckpointSequence);
                Assert.Equal(failed.LatestJournalSequence, failed.ProjectionFailureSequence!.Value);
                Assert.Equal("invalid", failed.ProjectionFailureKind);
                Assert.Equal(limits.ConfigurationFingerprint, failed.ProjectionFailureConfiguration);
                Assert.Equal(
                    WorkflowProjectionBuilder.SemanticFingerprint,
                    failed.ProjectionFailureSemantic);
                var writesAfterFirstFailure = Volatile.Read(ref writes);

                await Assert.ThrowsAsync<InvalidDataException>(() =>
                    recovered.GetWorkflowGraphAsync(
                        "project-a", "run-1", null, 100, null, 100,
                        TestContext.Current.CancellationToken));
                Assert.Equal(writesAfterFirstFailure, Volatile.Read(ref writes));
            }

            await SetProjectionInputBytesAsync(databasePath, original.ProjectionInputBytes);
            var changedLimits = new WorkflowProjectionLimits(maxNodes: 19_999);
            await using var retried = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: changedLimits);
            var rebuilt = await retried.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken);
            var recoveredRun = await retried.GetWorkflowRunAsync(
                "project-a", "run-1", TestContext.Current.CancellationToken);
            Assert.Equal(1UL, rebuilt!.JournalSequence);
            Assert.Null(recoveredRun!.ProjectionFailureSequence);
            Assert.Null(recoveredRun.ProjectionFailureKind);
            Assert.Null(recoveredRun.ProjectionFailureConfiguration);
            Assert.Null(recoveredRun.ProjectionFailureSemantic);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Invalid_base64_maps_to_contract_validation_without_echoing_content()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);
        const string invalidContent = "not-base64-sensitive-value";
        var contentRef =
            $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("unused")))}";
        var request = new WorkflowEventBatchAppendRequest
        {
            ClientId = "client-a",
            Events =
            [
                new WorkflowEventAppend
                {
                    EventId = "event-1",
                    SourceSequence = 1,
                    Timestamp = s_timestamp,
                    Kind = WorkflowJournalEventKind.ContentCaptured,
                    ContentRefs = [contentRef]
                }
            ],
            Content =
            [
                new WorkflowContentChunk
                {
                    ContentRef = contentRef,
                    ContentType = "application/octet-stream",
                    Encoding = WorkflowContentEncoding.Base64,
                    Content = invalidContent
                }
            ]
        };
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
        };
        context.Request.Headers["X-Qyl-Project"] = "project-a";
        context.Response.Body = new MemoryStream();

        var result = await CollectorEndpointExtensions.AppendEventsAsync(
            context,
            "run-1",
            request,
            store,
            TestContext.Current.CancellationToken);
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        var error = await JsonSerializer.DeserializeAsync(
            context.Response.Body,
            QylSerializerContext.Default.ValidationError,
            TestContext.Current.CancellationToken);
        var detail = Assert.Single(Assert.IsType<ValidationError>(error).Errors);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("content", detail.Field);
        Assert.Equal("content.base64.invalid", detail.Code);
        Assert.Null(detail.RejectedValue);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(
            context.Response.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);
        Assert.DoesNotContain(
            invalidContent,
            await reader.ReadToEndAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
        var page = await store.ReadWorkflowEventsAsync(
            "project-a", "run-1", 0, 10, TestContext.Current.CancellationToken);
        Assert.Empty(page!.Events);
    }

    private static async Task AssertProjectionFailureAsync(
        WorkflowProjectionLimits limits,
        IReadOnlyList<WorkflowEventWrite> events)
    {
        await using var store = new DuckDbStore(":memory:", workflowProjectionLimits: limits);
        await CreateRunAsync(store);
        var append = await store.AppendWorkflowEventsAsync(
            "project-a", "run-1", "client-a", events, [],
            TestContext.Current.CancellationToken);
        Assert.Equal(events.Count, append.AcceptedCount);
        await Assert.ThrowsAsync<WorkflowProjectionLimitExceededException>(() =>
            store.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken));
        Assert.Equal(
            (ulong)events.Count,
            (await store.GetWorkflowRunAsync(
                "project-a", "run-1", TestContext.Current.CancellationToken))!.LatestJournalSequence);
        Assert.Equal(
            events.Count,
            (await store.ReadWorkflowEventsAsync(
                "project-a", "run-1", 0, 100, TestContext.Current.CancellationToken))!.Events.Count);
    }

    private static Task<WorkflowRunStorageRow> CreateRunAsync(DuckDbStore store) =>
        store.CreateWorkflowRunAsync(
            new WorkflowRunStorageRow(
                "project-a",
                "run-1",
                "thread-1",
                "Projection budget fixture",
                WorkflowRunStatus.Active,
                s_timestamp,
                null,
                0,
                null,
                null),
            TestContext.Current.CancellationToken);

    private static WorkflowEventWrite Event(
        string eventId,
        ulong sourceSequence,
        WorkflowJournalEventKind kind = WorkflowJournalEventKind.ContentCaptured,
        string? attemptId = null,
        string? agentId = null,
        string? parentAgentId = null) =>
        new(
            eventId,
            sourceSequence,
            s_timestamp.AddMilliseconds(sourceSequence),
            kind,
            "thread-1",
            null,
            attemptId,
            agentId,
            parentAgentId,
            null,
            null,
            [],
            null);

    private static string DatabasePath(string testName) =>
        Path.Combine(Path.GetTempPath(), $"qyl-workflow-{testName}-{Guid.NewGuid():N}.duckdb");

    private static async Task SetProjectionInputBytesAsync(
        string databasePath,
        long projectionInputBytes)
    {
        await using var connection = new DuckDBConnection($"DataSource={databasePath}");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              UPDATE workflow_runs
                              SET projection_input_bytes = $1
                              WHERE project_id = 'project-a' AND run_id = 'run-1'
                              """;
        command.Parameters.Add(new DuckDBParameter { Value = projectionInputBytes });
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static void DeleteDatabase(string databasePath)
    {
        File.Delete(databasePath);
        File.Delete($"{databasePath}.wal");
        var checkpoints = $"{databasePath}.workflow-checkpoints";
        if (Directory.Exists(checkpoints))
            Directory.Delete(checkpoints, recursive: true);
    }
}
