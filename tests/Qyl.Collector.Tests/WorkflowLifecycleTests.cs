using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using DuckDB.NET.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Hosting;
using Qyl.Collector.Storage;
using Qyl.Collector.Telemetry;
using Qyl.Collector.Workflow;

namespace Qyl.Collector.Tests;

public sealed class WorkflowLifecycleTests
{
    private static readonly DateTimeOffset s_timestamp =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Checkpoint_publication_persists_exact_identity_for_first_and_advancing_heads()
    {
        var databasePath = DatabasePath("checkpoint-publication-identity");
        try
        {
            await using var store = new DuckDbStore(databasePath, maxConcurrentReads: 1);
            await CreateRunAsync(store, "run-1");
            await store.AppendWorkflowEventsAsync(
                "project-a", "run-1", "client-a", [Event("one", 1)], [],
                TestContext.Current.CancellationToken);
            Assert.NotNull(await store.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken));
            var first = await store.GetWorkflowRunAsync(
                "project-a", "run-1", TestContext.Current.CancellationToken);
            Assert.Equal(1UL, first!.ActiveCheckpointSequence);
            Assert.Equal(
                WorkflowCheckpointStore.CanonicalStorageIdentity(first),
                first.ActiveCheckpointStorageKey);
            Assert.True(first.CheckpointManifestEpoch > 0);

            await store.AppendWorkflowEventsAsync(
                "project-a", "run-1", "client-a", [Event("two", 2)], [],
                TestContext.Current.CancellationToken);
            Assert.NotNull(await store.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken));
            var second = await store.GetWorkflowRunAsync(
                "project-a", "run-1", TestContext.Current.CancellationToken);
            Assert.Equal(2UL, second!.ActiveCheckpointSequence);
            Assert.NotEqual(first.ActiveCheckpointId, second.ActiveCheckpointId);
            Assert.Equal(
                WorkflowCheckpointStore.CanonicalStorageIdentity(second),
                second.ActiveCheckpointStorageKey);
            Assert.True(
                second.CheckpointManifestEpoch >
                first.CheckpointManifestEpoch);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Startup_repairs_exact_manifest_triples_and_clears_partial_identity()
    {
        var databasePath = DatabasePath("checkpoint-manifest-triples");
        try
        {
            var manifests = new Dictionary<string, WorkflowRunStorageRow>(
                StringComparer.Ordinal);
            await using (var seed = new DuckDbStore(databasePath, maxConcurrentReads: 1))
            {
                foreach (var runId in new[] { "null-key", "stale-key", "forged-key", "partial" })
                {
                    await CreateRunAsync(seed, runId);
                    await seed.AppendWorkflowEventsAsync(
                        "project-a", runId, "client-a", [Event($"{runId}-one", 1)], [],
                        TestContext.Current.CancellationToken);
                    Assert.NotNull(await seed.GetWorkflowGraphAsync(
                        "project-a", runId, null, 100, null, 100,
                        TestContext.Current.CancellationToken));
                    manifests.Add(
                        runId,
                        (await seed.GetWorkflowRunAsync(
                            "project-a",
                            runId,
                            TestContext.Current.CancellationToken))!);
                }
            }

            await using (var connection = new DuckDBConnection($"DataSource={databasePath}"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var corrupt = connection.CreateCommand();
                corrupt.CommandText = """
                                      UPDATE workflow_runs
                                      SET active_checkpoint_storage_key =
                                          CASE run_id
                                              WHEN 'null-key' THEN NULL
                                              WHEN 'stale-key' THEN 'stale'
                                              WHEN 'forged-key' THEN $1
                                              ELSE active_checkpoint_storage_key
                                          END,
                                          active_checkpoint_id =
                                              CASE WHEN run_id = 'partial'
                                                  THEN NULL
                                                  ELSE active_checkpoint_id END
                                      WHERE project_id = 'project-a'
                                        AND run_id IN (
                                            'null-key',
                                            'stale-key',
                                            'forged-key',
                                            'partial')
                                      """;
                corrupt.Parameters.Add(new DuckDBParameter
                {
                    Value = manifests["null-key"].ActiveCheckpointStorageKey!
                });
                await corrupt.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            await using var store = new DuckDbStore(databasePath, maxConcurrentReads: 1);
            foreach (var runId in new[] { "null-key", "stale-key", "forged-key" })
            {
                var repaired = await store.GetWorkflowRunAsync(
                    "project-a", runId, TestContext.Current.CancellationToken);
                Assert.True(WorkflowCheckpointStore.HasCanonicalManifest(repaired!));
                Assert.Equal(
                    WorkflowCheckpointStore.CanonicalStorageIdentity(repaired),
                    repaired.ActiveCheckpointStorageKey);
            }
            var partial = await store.GetWorkflowRunAsync(
                "project-a", "partial", TestContext.Current.CancellationToken);
            Assert.Equal(0UL, partial!.ActiveCheckpointSequence);
            Assert.Null(partial.ActiveCheckpointId);
            Assert.Null(partial.ActiveCheckpointStorageKey);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Public_append_rejects_collector_control_client_identity()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store, "run-1");
        var context = EndpointContext();
        var result = await CollectorEndpointExtensions.AppendEventsAsync(
            context,
            "run-1",
            new WorkflowEventBatchAppendRequest
            {
                ClientId = "collector-control",
                Events =
                [
                    new WorkflowEventAppend
                    {
                        EventId = "forged-control",
                        SourceSequence = 1,
                        Timestamp = s_timestamp,
                        Kind = WorkflowJournalEventKind.ContentCaptured
                    }
                ]
            },
            store,
            TestContext.Current.CancellationToken);

        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var page = await store.ReadWorkflowEventsAsync(
            "project-a", "run-1", 0, 10, TestContext.Current.CancellationToken);
        Assert.Empty(page!.Events);
    }

    [Fact]
    public async Task Legacy_reconstruction_preserves_collector_control_acknowledgement_state()
    {
        var databasePath = DatabasePath("legacy-control-journal");
        try
        {
            await SeedLegacyWorkflowDatabaseAsync(databasePath);
            await using (var connection = new DuckDBConnection($"DataSource={databasePath}"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var insert = connection.CreateCommand();
                insert.CommandText = WorkflowEventDbRow.BuildMultiRowInsertSql(1);
                WorkflowEventDbRow.AddParameters(
                    insert,
                    new WorkflowEventDbRow
                    {
                        ProjectId = "project-a",
                        RunId = "run-1",
                        JournalSequence = 3,
                        EventId = "legacy-control",
                        ClientId = "collector-control",
                        SourceSequence = 7,
                        EventTime = s_timestamp.AddMilliseconds(3),
                        Kind = "control_requested",
                        ThreadId = "thread-1",
                        TurnId = null,
                        AttemptId = null,
                        AgentId = null,
                        ParentAgentId = null,
                        ReceiverAgentId = null,
                        ToolCallId = null,
                        ContentRefsJson = "[]",
                        DataJson = null
                    });
                await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            await using var store = new DuckDbStore(databasePath, maxConcurrentReads: 1);
            await using var verify = new DuckDBConnection($"DataSource={databasePath}");
            await verify.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = verify.CreateCommand();
            command.CommandText = """
                                  SELECT acknowledged_source_sequence
                                  FROM workflow_client_journal
                                  WHERE project_id = 'project-a'
                                    AND run_id = 'run-1'
                                    AND client_id = 'collector-control'
                                  """;
            Assert.Equal(
                0UL,
                Convert.ToUInt64(
                    await command.ExecuteScalarAsync(
                        TestContext.Current.CancellationToken)));
            command.CommandText = """
                                  SELECT count(*)
                                  FROM workflow_client_journal_ranges
                                  WHERE project_id = 'project-a'
                                    AND run_id = 'run-1'
                                    AND client_id = 'collector-control'
                                    AND range_start = 7
                                    AND range_end = 7
                                  """;
            Assert.Equal(
                1,
                Convert.ToInt32(
                    await command.ExecuteScalarAsync(
                        TestContext.Current.CancellationToken)));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Legacy_journal_state_is_backfilled_before_projection_tables_are_removed()
    {
        var databasePath = DatabasePath("legacy-backfill");
        var maximumRetainedRuns = 0;
        var retentionObservations = 0;
        try
        {
            await SeedLegacyWorkflowDatabaseAsync(databasePath);
            await AddLegacyRunsAsync(databasePath, 64);
            await using (var store = new DuckDbStore(
                             databasePath,
                             maxConcurrentReads: 1,
                             observeWorkflowMigrationRetainedRuns: retained =>
                             {
                                 Interlocked.Increment(ref retentionObservations);
                                 maximumRetainedRuns = Math.Max(
                                     maximumRetainedRuns,
                                     retained);
                             }))
            {
                var run = await store.GetWorkflowRunAsync(
                    "project-a", "run-1", TestContext.Current.CancellationToken);
                var events = await store.ReadWorkflowEventsAsync(
                    "project-a", "run-1", 0, 10,
                    TestContext.Current.CancellationToken);
                Assert.NotNull(run);
                Assert.Equal(2L, run.EventCount);
                Assert.Equal(2UL, run.LatestJournalSequence);
                Assert.Equal(5UL, run.NextCommandSequence);
                Assert.Equal(1UL, run.NextControlEventSourceSequence);
                Assert.Equal(
                    WorkflowCanonicalization.MeasureImmutableRunInput(run) +
                    WorkflowCanonicalization.MeasureDynamicRunInput(run) +
                    events!.Events.Sum(WorkflowCanonicalization.MeasureEventInput),
                    run.ProjectionInputBytes);
                Assert.Equal(
                    s_timestamp.AddDays(-40),
                    run.LastActivityAt);
                Assert.True(WorkflowCheckpointStore.IsCanonicalGeneration(
                    run.RunGeneration));

                var projected = await store.GetWorkflowGraphAsync(
                    "project-a", "run-1", null, 100, null, 100,
                    TestContext.Current.CancellationToken);
                Assert.Equal(2UL, projected!.JournalSequence);
                var gapFill = await store.AppendWorkflowEventsAsync(
                    "project-a",
                    "run-1",
                    "client-a",
                    [Event("source-two", 2)],
                    [],
                    TestContext.Current.CancellationToken);
                Assert.Equal(3UL, gapFill.AcknowledgedSourceSequence);
            }
            Assert.True(retentionObservations >= 130);
            Assert.Equal(1, maximumRetainedRuns);

            await using var connection = new DuckDBConnection($"DataSource={databasePath}");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT count(*)
                                  FROM information_schema.tables
                                  WHERE table_name IN (
                                      'workflow_projection_nodes',
                                      'workflow_projection_edges',
                                      'workflow_projection_state')
                                  """;
            Assert.Equal(
                0,
                Convert.ToInt32(
                    await command.ExecuteScalarAsync(
                        TestContext.Current.CancellationToken)));
            command.CommandText = """
                                  SELECT count(*)
                                  FROM information_schema.columns
                                  WHERE table_name = 'workflow_runs'
                                    AND column_name IN (
                                        'run_generation',
                                        'latest_journal_sequence',
                                        'event_count',
                                        'projection_input_bytes',
                                        'immutable_projection_input_bytes',
                                        'dynamic_projection_input_bytes',
                                        'next_command_sequence',
                                        'next_control_event_source_sequence',
                                        'active_checkpoint_sequence',
                                        'checkpoint_manifest_epoch',
                                        'last_activity_at')
                                    AND is_nullable <> 'NO'
                                  """;
            Assert.Equal(
                0,
                Convert.ToInt32(
                    await command.ExecuteScalarAsync(
                        TestContext.Current.CancellationToken)));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Persisted_path_components_are_repaired_without_escaping_checkpoint_root()
    {
        var databasePath = DatabasePath("path-containment");
        var sentinel = $"{databasePath}.sentinel";
        try
        {
            await using (var seed = new DuckDbStore(databasePath, maxConcurrentReads: 1))
            {
                await CreateRunAsync(seed, "run-1");
                await seed.AppendWorkflowEventsAsync(
                    "project-a", "run-1", "client-a", [Event("one", 1)], [],
                    TestContext.Current.CancellationToken);
                Assert.NotNull(await seed.GetWorkflowGraphAsync(
                    "project-a", "run-1", null, 100, null, 100,
                    TestContext.Current.CancellationToken));
            }
            await File.WriteAllTextAsync(
                sentinel,
                "outside-checkpoint-root",
                TestContext.Current.CancellationToken);
            await using (var connection = new DuckDBConnection($"DataSource={databasePath}"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                                      UPDATE workflow_runs
                                      SET run_generation = '../outside',
                                          active_checkpoint_id = $1,
                                          active_checkpoint_storage_key = $1
                                      WHERE project_id = 'project-a' AND run_id = 'run-1'
                                      """;
                command.Parameters.Add(new DuckDBParameter
                {
                    Value = Path.GetFullPath(sentinel)
                });
                await command.ExecuteNonQueryAsync(
                    TestContext.Current.CancellationToken);
            }

            await using var store = new DuckDbStore(databasePath, maxConcurrentReads: 1);
            var repaired = await store.GetWorkflowRunAsync(
                "project-a", "run-1", TestContext.Current.CancellationToken);
            Assert.True(File.Exists(sentinel));
            Assert.True(WorkflowCheckpointStore.IsCanonicalGeneration(
                repaired!.RunGeneration));
            Assert.Null(repaired.ActiveCheckpointId);
            Assert.Null(repaired.ActiveCheckpointStorageKey);
            Assert.False(WorkflowCheckpointStore.IsCanonicalGeneration("../outside"));
            Assert.False(WorkflowCheckpointStore.IsCanonicalGeneration("bad/name"));
            Assert.False(WorkflowCheckpointStore.IsCanonicalCheckpointId("../blob.json"));
            Assert.False(WorkflowCheckpointStore.IsCanonicalCheckpointId(
                Path.GetFullPath(sentinel)));
        }
        finally
        {
            File.Delete(sentinel);
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Checkpoint_operations_reject_root_child_symlink_without_touching_target()
    {
        if (OperatingSystem.IsWindows())
            return;

        var databasePath = DatabasePath("checkpoint-symlink");
        var outside = $"{databasePath}.outside";
        var sentinel = Path.Combine(outside, "sentinel.txt");
        try
        {
            Directory.CreateDirectory(outside);
            await File.WriteAllTextAsync(
                sentinel,
                "outside",
                TestContext.Current.CancellationToken);
            await using var store = new DuckDbStore(databasePath, maxConcurrentReads: 1);
            await CreateRunAsync(store, "run-1");
            var runStorageKey = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(
                    $"{9}:project-a{5}:run-1")));
            Directory.CreateSymbolicLink(
                Path.Combine(store.WorkflowCheckpointRoot!, runStorageKey),
                outside);

            await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
                store.GetWorkflowGraphAsync(
                    "project-a", "run-1", null, 100, null, 100,
                    TestContext.Current.CancellationToken));
            while (await store.ReconcileWorkflowCheckpointsAsync(
                       TestContext.Current.CancellationToken))
            {
            }

            Assert.Equal(
                "outside",
                await File.ReadAllTextAsync(
                    sentinel,
                    TestContext.Current.CancellationToken));
            Assert.Single(Directory.GetFileSystemEntries(outside));
        }
        finally
        {
            DeleteDatabase(databasePath);
            if (Directory.Exists(outside))
                Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task Sweep_child_swap_to_symlink_cannot_escape_pinned_checkpoint_root()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var databasePath = DatabasePath("checkpoint-symlink-race");
        var outside = $"{databasePath}.outside";
        var sentinel = Path.Combine(outside, "sentinel.txt");
        var swapped = 0;
        DuckDbStore? store = null;
        try
        {
            Directory.CreateDirectory(outside);
            await File.WriteAllTextAsync(
                sentinel,
                "outside",
                TestContext.Current.CancellationToken);
            store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                beforeCheckpointReconciliation: (stage, _) =>
                {
                    if (stage is WorkflowCheckpointReconciliationStage.SweepPrepared &&
                        Interlocked.Exchange(ref swapped, 1) is 0)
                    {
                        var child = Path.Combine(
                            store!.WorkflowCheckpointRoot!,
                            "race",
                            "child");
                        Directory.Move(child, $"{child}-original");
                        Directory.CreateSymbolicLink(child, outside);
                    }
                    return ValueTask.CompletedTask;
                });
            var child = Path.Combine(
                store.WorkflowCheckpointRoot!,
                "race",
                "child");
            Directory.CreateDirectory(child);
            await File.WriteAllBytesAsync(
                Path.Combine(child, "orphan.bin"),
                RandomNumberGenerator.GetBytes(32),
                TestContext.Current.CancellationToken);

            Assert.True(await store.ReconcileWorkflowCheckpointsAsync(
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                store.ReconcileWorkflowCheckpointsAsync(
                    TestContext.Current.CancellationToken));

            Assert.Equal(
                "outside",
                await File.ReadAllTextAsync(
                    sentinel,
                    TestContext.Current.CancellationToken));
            Assert.Single(Directory.GetFileSystemEntries(outside));
        }
        finally
        {
            if (store is not null)
                await store.DisposeAsync();
            DeleteDatabase(databasePath);
            if (Directory.Exists(outside))
                Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void Checkpoint_native_creation_is_fixed_signature_atomic_and_packaged()
    {
        const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static;
        var methods = typeof(WorkflowCheckpointFileSystem).GetMethods(Flags);
        Assert.Equal(
            2,
            Assert.Single(methods, static method => method.Name == "Open")
                .GetParameters()
                .Length);
        Assert.Equal(
            3,
            Assert.Single(methods, static method => method.Name == "OpenAt")
                .GetParameters()
                .Length);
        Assert.Equal(
            4,
            Assert.Single(methods, static method => method.Name == "OpenAtCreate")
                .GetParameters()
                .Length);

        var repositoryRoot = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(SourcePath())!,
            "..",
            ".."));
        var nativeSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "services",
            "qyl.collector",
            "Native",
            "qyl_checkpoint_native.c"));
        Assert.Contains("int qyl_openat_create(", nativeSource);
        Assert.Contains(
            "flags | QYL_O_CREAT | QYL_O_EXCL",
            nativeSource);
        Assert.Contains("(qyl_mode_t)mode", nativeSource);

        var managedSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "services",
            "qyl.collector",
            "Storage",
            "WorkflowCheckpointFileSystem.cs"));
        Assert.Contains(
            "LibraryImport(CheckpointNativeLibrary, EntryPoint = \"qyl_openat_create\"",
            managedSource);
        Assert.DoesNotContain("mknodat", managedSource);
        Assert.DoesNotContain("OpenCreate", managedSource);
        Assert.DoesNotContain("OpenExclusive", managedSource);

        var project = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "services",
            "qyl.collector",
            "qyl.collector.csproj"));
        Assert.Contains("linux-x64", project);
        Assert.Contains("linux-arm64", project);
        Assert.Contains("osx-x64", project);
        Assert.Contains("osx-arm64", project);
        Assert.Contains("QylCompileCheckpointNative", project);
        Assert.Contains(
            "BeforeTargets=\"CoreCompile;GetCopyToOutputDirectoryItems;GetCopyToPublishDirectoryItems\"",
            project);
        Assert.Contains(
            "<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>",
            project);
        Assert.Contains(
            "<CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>",
            project);
        Assert.Contains("-shared", project);
        Assert.Contains("-dynamiclib", project);

        var dockerfile = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "services",
            "qyl.collector",
            "Dockerfile"));
        Assert.Contains("libqyl_checkpoint_native.so", dockerfile);
    }

    [Fact]
    public async Task Dispose_closes_admission_before_cancelling_accepted_jobs()
    {
        var databasePath = DatabasePath("dispose-admission");
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        DuckDbStore? store = null;
        try
        {
            store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                beforeWrite: async token =>
                {
                    entered.TrySetResult(true);
                    await release.Task.WaitAsync(token);
                });
            var claimed = CreateRunAsync(store, "claimed");
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            var accepted = CreateRunAsync(store, "accepted");
            var disposal = store.DisposeAsync().AsTask();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => claimed);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => accepted);
            await disposal.WaitAsync(TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                CreateRunAsync(store, "rejected"));
        }
        finally
        {
            release.TrySetResult(true);
            if (store is not null)
                await store.DisposeAsync();
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Waiter_arriving_during_retirement_joins_then_readmits_current_generation()
    {
        var databasePath = DatabasePath("retirement-readmit");
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                beforeProjectionQuantum: async (_, _, token) =>
                {
                    if (Interlocked.Increment(ref calls) is 1)
                    {
                        entered.TrySetResult(true);
                        await release.Task.WaitAsync(token);
                    }
                });
            var run = await CreateRunAsync(store, "run-1");
            var first = store.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            var key = new WorkflowProjectionKey(
                "project-a",
                "run-1",
                run.RunGeneration);
            var retirement = store.RetireWorkflowProjectionAsync(key);
            var duringRetirement = store.WaitForWorkflowProjectionAsync(
                key,
                0,
                TestContext.Current.CancellationToken);
            Assert.False(retirement.IsCompleted);
            Assert.False(duringRetirement.IsCompleted);
            release.TrySetResult(true);

            await retirement.WaitAsync(TestContext.Current.CancellationToken);
            var checkpoint = await duringRetirement.WaitAsync(
                TestContext.Current.CancellationToken);
            Assert.Equal(0UL, checkpoint.JournalSequence);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => first);
        }
        finally
        {
            release.TrySetResult(true);
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Cancelled_ready_token_cannot_break_an_admitted_multi_quantum_requeue()
    {
        var databasePath = DatabasePath("reserved-requeue");
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(
                    maxRuntimeDemands: 2,
                    runtimeWorkerCount: 1,
                    runtimeEventQuantum: 1),
                beforeProjectionQuantum: async (key, target, token) =>
                {
                    if (key.RunId == "blocker" && target is 1)
                    {
                        entered.TrySetResult(true);
                        await release.Task.WaitAsync(token);
                    }
                });
            await CreateRunAsync(store, "blocker");
            await store.AppendWorkflowEventsAsync(
                "project-a",
                "blocker",
                "client-a",
                [Event("one", 1), Event("two", 2)],
                [],
                TestContext.Current.CancellationToken);
            var blocker = store.GetWorkflowGraphAsync(
                "project-a", "blocker", null, 100, null, 100,
                TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

            await CreateRunAsync(store, "cancelled");
            using var cancellation = new CancellationTokenSource();
            var cancelled = store.GetWorkflowGraphAsync(
                "project-a", "cancelled", null, 100, null, 100,
                cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            release.TrySetResult(true);

            var graph = await blocker.WaitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2UL, graph!.JournalSequence);
            Assert.Equal(0, store.WorkflowProjectionRuntimeSnapshot.AdmittedDemands);
        }
        finally
        {
            release.TrySetResult(true);
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Oversized_runtime_state_is_attempted_once()
    {
        var attempts = 0;
        await using var store = new DuckDbStore(
            ":memory:",
            workflowProjectionLimits: new WorkflowProjectionLimits(
                maxRuntimeCacheBytes: 1,
                runtimeWorkerCount: 1),
            beforeProjectionQuantum: (_, _, _) =>
            {
                Interlocked.Increment(ref attempts);
                return ValueTask.CompletedTask;
            });
        await CreateRunAsync(store, "run-1");

        await Assert.ThrowsAsync<WorkflowProjectionLimitExceededException>(() =>
            store.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Retention_uses_terminal_state_and_collector_activity_not_run_age()
    {
        var databasePath = DatabasePath("retention-activity");
        try
        {
            await using var store = new DuckDbStore(databasePath, maxConcurrentReads: 1);
            await CreateRunAsync(store, "old-active");
            await CreateRunAsync(store, "old-terminal");
            await CreateRunAsync(store, "recent-terminal");
            await store.AppendWorkflowEventsAsync(
                "project-a",
                "old-terminal",
                "client-a",
                [Event("completed-old", 1, WorkflowJournalEventKind.RunCompleted)],
                [],
                TestContext.Current.CancellationToken);
            await store.AppendWorkflowEventsAsync(
                "project-a",
                "recent-terminal",
                "client-a",
                [Event("completed-recent", 1, WorkflowJournalEventKind.RunCompleted)],
                [],
                TestContext.Current.CancellationToken);
            await SetLastActivityAsync(
                databasePath,
                ["old-active", "old-terminal"],
                s_timestamp.AddDays(-40));

            var deleted = await store.DeleteExpiredWorkflowDataBatchAsync(
                s_timestamp.AddDays(-30),
                100,
                TestContext.Current.CancellationToken);

            Assert.Equal(1, deleted.Runs);
            Assert.NotNull(await store.GetWorkflowRunAsync(
                "project-a", "old-active", TestContext.Current.CancellationToken));
            Assert.Null(await store.GetWorkflowRunAsync(
                "project-a", "old-terminal", TestContext.Current.CancellationToken));
            Assert.NotNull(await store.GetWorkflowRunAsync(
                "project-a", "recent-terminal", TestContext.Current.CancellationToken));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Retention_of_unprojected_run_during_reconciliation_emits_no_null_identity()
    {
        var databasePath = DatabasePath("retention-empty-manifest");
        try
        {
            await using var store = new DuckDbStore(databasePath, maxConcurrentReads: 1);
            await CreateRunAsync(store, "run-1");
            await using (var connection = new DuckDBConnection($"DataSource={databasePath}"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                                      UPDATE workflow_runs
                                      SET status = 'completed',
                                          ended_at = $1,
                                          last_activity_at = $2
                                      WHERE project_id = 'project-a'
                                        AND run_id = 'run-1'
                                      """;
                command.Parameters.Add(new DuckDBParameter
                {
                    Value = s_timestamp.UtcDateTime
                });
                command.Parameters.Add(new DuckDBParameter
                {
                    Value = s_timestamp.AddDays(-40).UtcDateTime
                });
                await command.ExecuteNonQueryAsync(
                    TestContext.Current.CancellationToken);
            }

            Assert.True(await store.ReconcileWorkflowCheckpointsAsync(
                TestContext.Current.CancellationToken));
            var deleted = await store.DeleteExpiredWorkflowDataBatchAsync(
                s_timestamp.AddDays(-30),
                1,
                TestContext.Current.CancellationToken);
            while (await store.ReconcileWorkflowCheckpointsAsync(
                       TestContext.Current.CancellationToken))
            {
            }

            Assert.Equal(new WorkflowRetentionResult(1, 0, 0, 0), deleted);
            Assert.Null(await store.GetWorkflowRunAsync(
                "project-a", "run-1", TestContext.Current.CancellationToken));
            var metrics = store.GetStorageFileMetrics();
            Assert.Equal(0, metrics.LiveCheckpointBytes);
            Assert.Equal(0, metrics.TemporaryOrOrphanCheckpointBytes);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Write_cancellation_before_claim_has_no_effect_and_after_claim_awaits_commit()
    {
        var queuedDatabase = DatabasePath("cancel-before-claim");
        var claimedDatabase = DatabasePath("cancel-after-claim");
        try
        {
            var firstClaimed = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var claims = 0;
            await using (var store = new DuckDbStore(
                             queuedDatabase,
                             jobQueueCapacity: 2,
                             maxConcurrentReads: 1,
                             beforeWrite: async token =>
                             {
                                 if (Interlocked.Increment(ref claims) is 1)
                                 {
                                     firstClaimed.TrySetResult(true);
                                     await releaseFirst.Task.WaitAsync(token);
                                 }
                             }))
            {
                var blocker = CreateRunAsync(store, "blocker");
                await firstClaimed.Task.WaitAsync(TestContext.Current.CancellationToken);
                using var queuedCancellation = new CancellationTokenSource();
                var cancelled = CreateRunAsync(
                    store,
                    "cancelled-before-claim",
                    queuedCancellation.Token);
                queuedCancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
                releaseFirst.TrySetResult(true);
                await blocker;
                Assert.Null(await store.GetWorkflowRunAsync(
                    "project-a",
                    "cancelled-before-claim",
                    TestContext.Current.CancellationToken));
            }

            var claimed = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseClaimed = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            await using (var store = new DuckDbStore(
                             claimedDatabase,
                             maxConcurrentReads: 1,
                             beforeWrite: async token =>
                             {
                                 claimed.TrySetResult(true);
                                 await releaseClaimed.Task.WaitAsync(token);
                             }))
            {
                using var cancellation = new CancellationTokenSource();
                var create = CreateRunAsync(
                    store,
                    "committed-after-claim",
                    cancellation.Token);
                await claimed.Task.WaitAsync(TestContext.Current.CancellationToken);
                cancellation.Cancel();
                Assert.False(create.IsCompleted);
                releaseClaimed.TrySetResult(true);
                var created = await create;
                var persisted = await store.GetWorkflowRunAsync(
                    "project-a",
                    "committed-after-claim",
                    TestContext.Current.CancellationToken);
                Assert.Equal(created.RunGeneration, persisted!.RunGeneration);
                Assert.Null(persisted.ActiveCheckpointId);
                Assert.NotNull(await store.GetWorkflowGraphAsync(
                    "project-a",
                    "committed-after-claim",
                    null,
                    100,
                    null,
                    100,
                    TestContext.Current.CancellationToken));
            }
        }
        finally
        {
            DeleteDatabase(queuedDatabase);
            DeleteDatabase(claimedDatabase);
        }
    }

    [Fact]
    public async Task Exact_head_waiter_completes_while_a_newer_demand_remains_in_flight()
    {
        var databasePath = DatabasePath("exact-head-waiter");
        var firstEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecond = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(runtimeEventQuantum: 8),
                beforeProjectionQuantum: async (_, target, token) =>
                {
                    if (target is 1)
                    {
                        firstEntered.TrySetResult(true);
                        await releaseFirst.Task.WaitAsync(token);
                    }
                    else if (target is 2)
                    {
                        secondEntered.TrySetResult(true);
                        await releaseSecond.Task.WaitAsync(token);
                    }
                });
            await CreateRunAsync(store, "run-1");
            await store.AppendWorkflowEventsAsync(
                "project-a", "run-1", "client-a", [Event("one", 1)], [],
                TestContext.Current.CancellationToken);
            await firstEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
            var headOne = store.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken);
            await store.AppendWorkflowEventsAsync(
                "project-a", "run-1", "client-a", [Event("two", 2)], [],
                TestContext.Current.CancellationToken);
            releaseFirst.TrySetResult(true);
            await secondEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

            var exact = await headOne.WaitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1UL, exact!.JournalSequence);
            Assert.False(releaseSecond.Task.IsCompleted);
            releaseSecond.TrySetResult(true);
            var latest = await store.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken);
            Assert.Equal(2UL, latest!.JournalSequence);
        }
        finally
        {
            releaseFirst.TrySetResult(true);
            releaseSecond.TrySetResult(true);
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task No_content_projection_is_estimated_and_cached()
    {
        await using var store = new DuckDbStore(
            ":memory:",
            workflowProjectionLimits: new WorkflowProjectionLimits(runtimeWorkerCount: 1));
        await CreateRunAsync(store, "run-1");

        var graph = await store.GetWorkflowGraphAsync(
            "project-a", "run-1", null, 100, null, 100,
            TestContext.Current.CancellationToken);

        Assert.Equal(0UL, graph!.JournalSequence);
        Assert.Null(Assert.Single(graph.Nodes).ContentRefs);
        var snapshot = store.WorkflowProjectionRuntimeSnapshot;
        Assert.Equal(1, snapshot.CachedStates);
        Assert.True(snapshot.CachedBytes > 0);
    }

    [Fact]
    public async Task Head_zero_waiter_is_served_before_a_newer_coalesced_demand()
    {
        var databasePath = DatabasePath("head-zero");
        var blockerEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBlocker = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var headZeroSelected = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(runtimeWorkerCount: 1),
                beforeProjectionQuantum: async (key, target, token) =>
                {
                    if (key.RunId == "blocker")
                    {
                        blockerEntered.TrySetResult(true);
                        await releaseBlocker.Task.WaitAsync(token);
                    }
                    else if (key.RunId == "target" && target is 0)
                    {
                        headZeroSelected.TrySetResult(true);
                    }
                });
            await CreateRunAsync(store, "blocker");
            await CreateRunAsync(store, "target");
            await store.AppendWorkflowEventsAsync(
                "project-a", "blocker", "client-a", [Event("blocker", 1)], [],
                TestContext.Current.CancellationToken);
            await blockerEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

            var headZero = store.GetWorkflowGraphAsync(
                "project-a", "target", null, 100, null, 100,
                TestContext.Current.CancellationToken);
            await WaitUntilAsync(() => Task.FromResult(
                store.WorkflowProjectionRuntimeSnapshot.AdmittedDemands is 2));
            await store.AppendWorkflowEventsAsync(
                "project-a", "target", "client-a", [Event("newer", 1)], [],
                TestContext.Current.CancellationToken);
            releaseBlocker.TrySetResult(true);

            await headZeroSelected.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(
                0UL,
                (await headZero.WaitAsync(TestContext.Current.CancellationToken))!.JournalSequence);
            Assert.Equal(
                1UL,
                (await store.GetWorkflowGraphAsync(
                    "project-a", "target", null, 100, null, 100,
                    TestContext.Current.CancellationToken))!.JournalSequence);
        }
        finally
        {
            releaseBlocker.TrySetResult(true);
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Cancelled_processing_waiter_does_not_complete_retirement_before_worker_exit()
    {
        var databasePath = DatabasePath("cancel-retire-join");
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                beforeProjectionQuantum: async (_, target, token) =>
                {
                    if (target is 0)
                    {
                        entered.TrySetResult(true);
                        await release.Task.WaitAsync(token);
                    }
                });
            var run = await CreateRunAsync(store, "run-1");
            using var cancellation = new CancellationTokenSource();
            var graph = store.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100, cancellation.Token);
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => graph);

            var retirement = store.RetireWorkflowProjectionAsync(
                new WorkflowProjectionKey("project-a", "run-1", run.RunGeneration));
            Assert.False(retirement.IsCompleted);
            release.TrySetResult(true);
            await retirement.WaitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, store.WorkflowProjectionRuntimeSnapshot.AdmittedDemands);
        }
        finally
        {
            release.TrySetResult(true);
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Retired_ready_token_cannot_execute_a_readmitted_demand()
    {
        var databasePath = DatabasePath("ready-demand-identity");
        var blockerAEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var blockerBEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBlockerA = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBlockerB = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var targetEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var duplicateTargetEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTarget = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var sentinelEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var targetWorkers = 0;
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(runtimeWorkerCount: 2),
                beforeProjectionQuantum: async (key, _, token) =>
                {
                    if (key.RunId == "blocker-a")
                    {
                        blockerAEntered.TrySetResult(true);
                        await releaseBlockerA.Task.WaitAsync(token);
                    }
                    else if (key.RunId == "blocker-b")
                    {
                        blockerBEntered.TrySetResult(true);
                        await releaseBlockerB.Task.WaitAsync(token);
                    }
                    else if (key.RunId == "target")
                    {
                        if (Interlocked.Increment(ref targetWorkers) is 1)
                            targetEntered.TrySetResult(true);
                        else
                            duplicateTargetEntered.TrySetResult(true);
                        try
                        {
                            await releaseTarget.Task.WaitAsync(token);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref targetWorkers);
                        }
                    }
                    else if (key.RunId == "sentinel")
                    {
                        sentinelEntered.TrySetResult(true);
                    }
                });
            foreach (var runId in new[] { "blocker-a", "blocker-b", "target", "sentinel" })
                await CreateRunAsync(store, runId);
            await store.AppendWorkflowEventsAsync(
                "project-a", "blocker-a", "client-a", [Event("a", 1)], [],
                TestContext.Current.CancellationToken);
            await store.AppendWorkflowEventsAsync(
                "project-a", "blocker-b", "client-a", [Event("b", 1)], [],
                TestContext.Current.CancellationToken);
            await Task.WhenAll(blockerAEntered.Task, blockerBEntered.Task)
                .WaitAsync(TestContext.Current.CancellationToken);

            using var staleCancellation = new CancellationTokenSource();
            var stale = store.GetWorkflowGraphAsync(
                "project-a", "target", null, 100, null, 100, staleCancellation.Token);
            await WaitUntilAsync(() => Task.FromResult(
                store.WorkflowProjectionRuntimeSnapshot.AdmittedDemands is 3));
            staleCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stale);
            await WaitUntilAsync(() => Task.FromResult(
                store.WorkflowProjectionRuntimeSnapshot.AdmittedDemands is 2));

            var current = store.GetWorkflowGraphAsync(
                "project-a", "target", null, 100, null, 100,
                TestContext.Current.CancellationToken);
            await WaitUntilAsync(() => Task.FromResult(
                store.WorkflowProjectionRuntimeSnapshot.AdmittedDemands is 3));
            releaseBlockerA.TrySetResult(true);
            await targetEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
            await store.AppendWorkflowEventsAsync(
                "project-a", "sentinel", "client-a", [Event("sentinel", 1)], [],
                TestContext.Current.CancellationToken);
            releaseBlockerB.TrySetResult(true);

            var winner = await Task.WhenAny(
                    sentinelEntered.Task,
                    duplicateTargetEntered.Task)
                .WaitAsync(TestContext.Current.CancellationToken);
            Assert.Same(sentinelEntered.Task, winner);
            releaseTarget.TrySetResult(true);
            Assert.Equal(
                0UL,
                (await current.WaitAsync(TestContext.Current.CancellationToken))!.JournalSequence);
        }
        finally
        {
            releaseBlockerA.TrySetResult(true);
            releaseBlockerB.TrySetResult(true);
            releaseTarget.TrySetResult(true);
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Transient_projection_faults_retry_without_poison_and_deterministic_faults_persist()
    {
        var transientDatabase = DatabasePath("transient-projection");
        var deterministicDatabase = DatabasePath("deterministic-projection");
        try
        {
            var attempts = 0;
            await using (var transient = new DuckDbStore(
                             transientDatabase,
                             maxConcurrentReads: 1,
                             beforeProjectionQuantum: (_, _, _) =>
                             {
                                 Interlocked.Increment(ref attempts);
                                 return ValueTask.FromException(
                                     new IOException("transient checkpoint storage fault"));
                             }))
            {
                await CreateRunAsync(transient, "run-1");
                await transient.AppendWorkflowEventsAsync(
                    "project-a", "run-1", "client-a", [Event("one", 1)], [],
                    TestContext.Current.CancellationToken);
                await Assert.ThrowsAsync<IOException>(() =>
                    transient.GetWorkflowGraphAsync(
                        "project-a", "run-1", null, 100, null, 100,
                        TestContext.Current.CancellationToken));
                var run = await transient.GetWorkflowRunAsync(
                    "project-a", "run-1", TestContext.Current.CancellationToken);
                Assert.Null(run!.ProjectionFailureSequence);
                Assert.True(Volatile.Read(ref attempts) >= 4);
            }

            await using (var deterministic = new DuckDbStore(
                             deterministicDatabase,
                             maxConcurrentReads: 1,
                             beforeProjectionQuantum: (_, _, _) =>
                                 ValueTask.FromException(
                                     new InvalidDataException("deterministic projection input"))))
            {
                await CreateRunAsync(deterministic, "run-1");
                await deterministic.AppendWorkflowEventsAsync(
                    "project-a", "run-1", "client-a", [Event("one", 1)], [],
                    TestContext.Current.CancellationToken);
                await Assert.ThrowsAsync<InvalidDataException>(() =>
                    deterministic.GetWorkflowGraphAsync(
                        "project-a", "run-1", null, 100, null, 100,
                        TestContext.Current.CancellationToken));
                var failed = await deterministic.GetWorkflowRunAsync(
                    "project-a", "run-1", TestContext.Current.CancellationToken);
                Assert.Equal("invalid", failed!.ProjectionFailureKind);
                Assert.Equal(
                    WorkflowProjectionBuilder.SemanticFingerprint,
                    failed.ProjectionFailureSemantic);
            }
        }
        finally
        {
            DeleteDatabase(transientDatabase);
            DeleteDatabase(deterministicDatabase);
        }
    }

    [Fact]
    public async Task Delete_recreate_generation_rejects_the_stale_worker_publication()
    {
        var databasePath = DatabasePath("generation-aba");
        var oldWorkerEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOldWorker = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        string? oldGeneration = null;
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                beforeProjectionQuantum: async (key, _, token) =>
                {
                    if (key.RunGeneration == oldGeneration)
                    {
                        oldWorkerEntered.TrySetResult(true);
                        await releaseOldWorker.Task.WaitAsync(token);
                    }
                });
            var oldRun = await CreateRunAsync(store, "run-1");
            oldGeneration = oldRun.RunGeneration;
            await store.AppendWorkflowEventsAsync(
                "project-a",
                "run-1",
                "client-a",
                [Event("completed", 1, WorkflowJournalEventKind.RunCompleted)],
                [],
                TestContext.Current.CancellationToken);
            await oldWorkerEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
            await SetLastActivityAsync(
                databasePath,
                ["run-1"],
                s_timestamp.AddDays(-40));

            var retention = store.DeleteExpiredWorkflowDataBatchAsync(
                s_timestamp.AddDays(-30),
                100,
                TestContext.Current.CancellationToken);
            await WaitUntilAsync(
                async () => await store.GetWorkflowRunAsync(
                    "project-a", "run-1", TestContext.Current.CancellationToken) is null);
            var recreated = await CreateRunAsync(store, "run-1");
            Assert.NotEqual(oldGeneration, recreated.RunGeneration);
            releaseOldWorker.TrySetResult(true);
            Assert.Equal(1, (await retention).Runs);

            var current = await store.GetWorkflowRunAsync(
                "project-a", "run-1", TestContext.Current.CancellationToken);
            Assert.Equal(recreated.RunGeneration, current!.RunGeneration);
            Assert.Null(current.ActiveCheckpointId);
            Assert.Equal(0UL, current.ActiveCheckpointSequence);
        }
        finally
        {
            releaseOldWorker.TrySetResult(true);
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Reconciliation_removes_crash_residue_and_rebuilds_a_missing_manifest_blob()
    {
        var databasePath = DatabasePath("checkpoint-reconcile");
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(
                    checkpointSweepLimit: 1));
            await CreateRunAsync(store, "run-1");
            await store.AppendWorkflowEventsAsync(
                "project-a", "run-1", "client-a", [Event("one", 1)], [],
                TestContext.Current.CancellationToken);
            Assert.NotNull(await store.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken));
            var root = store.WorkflowCheckpointRoot!;
            var live = Assert.Single(Directory.GetFiles(
                root, "*.json", SearchOption.AllDirectories));
            var residueDirectory = Path.Combine(root, "crash", "generation");
            Directory.CreateDirectory(residueDirectory);
            var emptyGeneration = Path.Combine(root, "empty-run", "empty-generation");
            Directory.CreateDirectory(emptyGeneration);
            var temporary = Path.Combine(residueDirectory, ".crash.tmp");
            var orphan = Path.Combine(residueDirectory, "orphan.json");
            await File.WriteAllBytesAsync(
                temporary,
                RandomNumberGenerator.GetBytes(127),
                TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(
                orphan,
                RandomNumberGenerator.GetBytes(251),
                TestContext.Current.CancellationToken);
            File.SetLastWriteTimeUtc(temporary, DateTime.UtcNow.AddHours(-1));
            File.Delete(live);

            while (await store.ReconcileWorkflowCheckpointsAsync(
                       TestContext.Current.CancellationToken))
            {
            }
            Assert.False(File.Exists(temporary));
            Assert.False(File.Exists(orphan));
            Assert.False(Directory.Exists(emptyGeneration));
            Assert.False(Directory.Exists(Path.GetDirectoryName(emptyGeneration)));
            var rebuilt = await store.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken);
            Assert.Equal(1UL, rebuilt!.JournalSequence);
            Assert.Single(Directory.GetFiles(
                root, "*.json", SearchOption.AllDirectories));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Reconciliation_never_deletes_a_checkpoint_published_between_manifest_pages()
    {
        var databasePath = DatabasePath("checkpoint-publication-race");
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(
                    checkpointSweepLimit: 1,
                    runtimeWorkerCount: 1));
            foreach (var runId in new[] { "a-run", "b-run" })
            {
                await CreateRunAsync(store, runId);
                await store.AppendWorkflowEventsAsync(
                    "project-a", runId, "client-a", [Event($"{runId}-one", 1)], [],
                    TestContext.Current.CancellationToken);
                await store.GetWorkflowGraphAsync(
                    "project-a", runId, null, 100, null, 100,
                    TestContext.Current.CancellationToken);
            }

            Assert.True(await store.ReconcileWorkflowCheckpointsAsync(
                TestContext.Current.CancellationToken));
            await store.AppendWorkflowEventsAsync(
                "project-a", "a-run", "client-a", [Event("a-run-two", 2)], [],
                TestContext.Current.CancellationToken);
            await store.GetWorkflowGraphAsync(
                "project-a", "a-run", null, 100, null, 100,
                TestContext.Current.CancellationToken);
            while (await store.ReconcileWorkflowCheckpointsAsync(
                       TestContext.Current.CancellationToken))
            {
            }

            var run = await store.GetWorkflowRunAsync(
                "project-a", "a-run", TestContext.Current.CancellationToken);
            Assert.NotNull(run!.ActiveCheckpointId);
            Assert.Single(Directory.GetFiles(
                store.WorkflowCheckpointRoot!,
                run.ActiveCheckpointId,
                SearchOption.AllDirectories));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Reconciliation_deletes_an_active_filename_copied_to_the_wrong_generation()
    {
        var databasePath = DatabasePath("checkpoint-wrong-generation");
        try
        {
            await using var store = new DuckDbStore(databasePath, maxConcurrentReads: 1);
            await CreateRunAsync(store, "run-1");
            await store.AppendWorkflowEventsAsync(
                "project-a", "run-1", "client-a", [Event("one", 1)], [],
                TestContext.Current.CancellationToken);
            Assert.NotNull(await store.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken));

            var active = Assert.Single(Directory.GetFiles(
                store.WorkflowCheckpointRoot!,
                "*.json",
                SearchOption.AllDirectories));
            var generationDirectory = Path.GetDirectoryName(active)!;
            var runDirectory = Path.GetDirectoryName(generationDirectory)!;
            var wrongGenerationDirectory = Path.Combine(
                runDirectory,
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(wrongGenerationDirectory);
            var copiedOrphan = Path.Combine(
                wrongGenerationDirectory,
                Path.GetFileName(active));
            File.Copy(active, copiedOrphan);

            while (await store.ReconcileWorkflowCheckpointsAsync(
                       TestContext.Current.CancellationToken))
            {
            }

            Assert.True(File.Exists(active));
            Assert.False(File.Exists(copiedOrphan));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Reconciliation_pages_are_bounded_and_resume_atomically_after_failure()
    {
        var databasePath = DatabasePath("checkpoint-resume");
        var armed = 0;
        var failed = 0;
        var manifestPages = 0;
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(
                    checkpointSweepLimit: 1,
                    checkpointReconciliationByteLimit: 1),
                beforeCheckpointReconciliation: (stage, _) =>
                {
                    if (stage is WorkflowCheckpointReconciliationStage.ManifestValidated)
                        Interlocked.Increment(ref manifestPages);
                    if (stage is WorkflowCheckpointReconciliationStage.SweepMetadataRead &&
                        Volatile.Read(ref armed) is 1 &&
                        Interlocked.Exchange(ref failed, 1) is 0)
                    {
                        throw new IOException("transient reconciliation fixture");
                    }
                    return ValueTask.CompletedTask;
                });
            foreach (var runId in new[] { "run-1", "run-2" })
            {
                await CreateRunAsync(store, runId);
                await store.AppendWorkflowEventsAsync(
                    "project-a", runId, "client-a", [Event($"{runId}-one", 1)], [],
                    TestContext.Current.CancellationToken);
                Assert.NotNull(await store.GetWorkflowGraphAsync(
                    "project-a", runId, null, 100, null, 100,
                    TestContext.Current.CancellationToken));
            }

            var orphanDirectory = Path.Combine(
                store.WorkflowCheckpointRoot!,
                "orphan-run",
                "orphan-generation");
            Directory.CreateDirectory(orphanDirectory);
            var orphan = Path.Combine(orphanDirectory, "orphan.json");
            await File.WriteAllBytesAsync(
                orphan,
                RandomNumberGenerator.GetBytes(127),
                TestContext.Current.CancellationToken);
            Volatile.Write(ref armed, 1);

            while (true)
            {
                try
                {
                    if (!await store.ReconcileWorkflowCheckpointsAsync(
                            TestContext.Current.CancellationToken))
                    {
                        break;
                    }
                }
                catch (IOException)
                {
                }
            }

            Assert.Equal(1, failed);
            Assert.True(manifestPages >= 2);
            Assert.False(File.Exists(orphan));
            Assert.False(Directory.Exists(orphanDirectory));
            var files = Directory.GetFiles(
                store.WorkflowCheckpointRoot!,
                "*",
                SearchOption.AllDirectories);
            var actualBytes = files.Sum(static file => new FileInfo(file).Length);
            var metrics = store.GetStorageFileMetrics();
            Assert.Equal(actualBytes, metrics.LiveCheckpointBytes);
            Assert.Equal(0, metrics.TemporaryOrOrphanCheckpointBytes);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Quarantined_sweep_deletion_does_not_block_publication_or_lose_metric_deltas()
    {
        var databasePath = DatabasePath("checkpoint-quarantine-publication");
        var armed = 0;
        var claimed = 0;
        var deletionEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDeletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                beforeCheckpointReconciliation: async (stage, token) =>
                {
                    if (stage is WorkflowCheckpointReconciliationStage.SweepClaimed &&
                        Volatile.Read(ref armed) is 1 &&
                        Interlocked.Exchange(ref claimed, 1) is 0)
                    {
                        deletionEntered.TrySetResult(true);
                        await releaseDeletion.Task.WaitAsync(token);
                    }
                });
            await CreateRunAsync(store, "existing");
            await store.AppendWorkflowEventsAsync(
                "project-a", "existing", "client-a", [Event("existing-one", 1)], [],
                TestContext.Current.CancellationToken);
            Assert.NotNull(await store.GetWorkflowGraphAsync(
                "project-a", "existing", null, 100, null, 100,
                TestContext.Current.CancellationToken));
            var orphan = Path.Combine(store.WorkflowCheckpointRoot!, "orphan.bin");
            await File.WriteAllBytesAsync(
                orphan,
                RandomNumberGenerator.GetBytes(257),
                TestContext.Current.CancellationToken);
            Volatile.Write(ref armed, 1);

            var reconciliation = Task.Run(async () =>
            {
                while (await store.ReconcileWorkflowCheckpointsAsync(
                           TestContext.Current.CancellationToken))
                {
                }
            }, TestContext.Current.CancellationToken);
            await deletionEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

            await CreateRunAsync(store, "concurrent");
            await store.AppendWorkflowEventsAsync(
                "project-a", "concurrent", "client-a", [Event("concurrent-one", 1)], [],
                TestContext.Current.CancellationToken);
            Assert.NotNull(await store.GetWorkflowGraphAsync(
                "project-a", "concurrent", null, 100, null, 100,
                TestContext.Current.CancellationToken));

            releaseDeletion.TrySetResult(true);
            await reconciliation.WaitAsync(TestContext.Current.CancellationToken);
            var files = Directory.GetFiles(
                store.WorkflowCheckpointRoot!,
                "*",
                SearchOption.AllDirectories);
            var exactLiveBytes = files.Sum(static file => new FileInfo(file).Length);
            var metrics = store.GetStorageFileMetrics();
            Assert.False(File.Exists(orphan));
            Assert.Equal(exactLiveBytes, metrics.LiveCheckpointBytes);
            Assert.Equal(0, metrics.TemporaryOrOrphanCheckpointBytes);
        }
        finally
        {
            releaseDeletion.TrySetResult(true);
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Reconciliation_epoch_folds_page_publication_replacement_and_retirement_once()
    {
        var databasePath = DatabasePath("checkpoint-reconciliation-epoch");
        var armed = 0;
        var sweepPage = 0;
        DuckDbStore? store = null;
        try
        {
            store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(
                    checkpointSweepLimit: 1,
                    runtimeWorkerCount: 1),
                beforeCheckpointReconciliation: async (stage, token) =>
                {
                    if (stage is not WorkflowCheckpointReconciliationStage.SweepPrepared ||
                        Volatile.Read(ref armed) is 0)
                    {
                        return;
                    }

                    switch (Interlocked.Increment(ref sweepPage))
                    {
                        case 1:
                            await store!.AppendWorkflowEventsAsync(
                                "project-a",
                                "before",
                                "client-a",
                                [Event("before-two", 2)],
                                [],
                                token);
                            Assert.NotNull(await store.GetWorkflowGraphAsync(
                                "project-a",
                                "before",
                                null,
                                100,
                                null,
                                100,
                                token));
                            break;
                        case 2:
                            await CreateRunAsync(store!, "after", token);
                            await store!.AppendWorkflowEventsAsync(
                                "project-a",
                                "after",
                                "client-a",
                                [Event("after-one", 1)],
                                [],
                                token);
                            Assert.NotNull(await store.GetWorkflowGraphAsync(
                                "project-a",
                                "after",
                                null,
                                100,
                                null,
                                100,
                                token));
                            break;
                        case 3:
                            var deleted = await store!
                                .DeleteExpiredWorkflowDataBatchAsync(
                                    s_timestamp.AddDays(-30),
                                    10,
                                    token);
                            Assert.Equal(1, deleted.Runs);
                            break;
                    }
                });

            await CreateRunAsync(store, "before");
            await store.AppendWorkflowEventsAsync(
                "project-a",
                "before",
                "client-a",
                [Event("before-one", 1)],
                [],
                TestContext.Current.CancellationToken);
            Assert.NotNull(await store.GetWorkflowGraphAsync(
                "project-a",
                "before",
                null,
                100,
                null,
                100,
                TestContext.Current.CancellationToken));

            await CreateRunAsync(store, "retired");
            await store.AppendWorkflowEventsAsync(
                "project-a",
                "retired",
                "client-a",
                [Event("retired", 1, WorkflowJournalEventKind.RunCompleted)],
                [],
                TestContext.Current.CancellationToken);
            Assert.NotNull(await store.GetWorkflowGraphAsync(
                "project-a",
                "retired",
                null,
                100,
                null,
                100,
                TestContext.Current.CancellationToken));
            await SetLastActivityAsync(
                databasePath,
                ["retired"],
                s_timestamp.AddDays(-40));

            while (await store.ReconcileWorkflowCheckpointsAsync(
                       TestContext.Current.CancellationToken))
            {
            }

            var residue = Path.Combine(
                store.WorkflowCheckpointRoot!,
                "epoch-residue");
            Directory.CreateDirectory(residue);
            for (var index = 0; index < 3; index++)
            {
                await File.WriteAllBytesAsync(
                    Path.Combine(residue, $"orphan-{index}.bin"),
                    RandomNumberGenerator.GetBytes(31 + index),
                    TestContext.Current.CancellationToken);
            }
            Volatile.Write(ref armed, 1);

            while (await store.ReconcileWorkflowCheckpointsAsync(
                       TestContext.Current.CancellationToken))
            {
            }

            Assert.True(Volatile.Read(ref sweepPage) >= 3);
            var before = await store.GetWorkflowRunAsync(
                "project-a",
                "before",
                TestContext.Current.CancellationToken);
            var after = await store.GetWorkflowRunAsync(
                "project-a",
                "after",
                TestContext.Current.CancellationToken);
            Assert.Equal(2UL, before!.ActiveCheckpointSequence);
            Assert.Equal(1UL, after!.ActiveCheckpointSequence);
            Assert.Null(await store.GetWorkflowRunAsync(
                "project-a",
                "retired",
                TestContext.Current.CancellationToken));

            var activeIds = new HashSet<string>(
                [before.ActiveCheckpointId!, after.ActiveCheckpointId!],
                StringComparer.Ordinal);
            var files = Directory.GetFiles(
                store.WorkflowCheckpointRoot!,
                "*",
                SearchOption.AllDirectories);
            var totalBytes = files.Sum(static file => new FileInfo(file).Length);
            var liveBytes = files
                .Where(file => activeIds.Contains(Path.GetFileName(file)))
                .Sum(static file => new FileInfo(file).Length);
            var metrics = store.GetStorageFileMetrics();
            Assert.Equal(liveBytes, metrics.LiveCheckpointBytes);
            Assert.Equal(
                totalBytes - liveBytes,
                metrics.TemporaryOrOrphanCheckpointBytes);
        }
        finally
        {
            if (store is not null)
                await store.DisposeAsync();
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Broken_manifest_repair_does_not_clear_a_newer_publication()
    {
        var databasePath = DatabasePath("manifest-repair-cas");
        var armed = 0;
        DuckDbStore? store = null;
        try
        {
            store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                beforeCheckpointReconciliation: async (stage, token) =>
                {
                    if (stage is WorkflowCheckpointReconciliationStage.ManifestValidated &&
                        Interlocked.Exchange(ref armed, 0) is 1)
                    {
                        await store!.AppendWorkflowEventsAsync(
                            "project-a",
                            "run-1",
                            "client-a",
                            [Event("two", 2)],
                            [],
                            token);
                        Assert.NotNull(await store.GetWorkflowGraphAsync(
                            "project-a", "run-1", null, 100, null, 100, token));
                    }
                });
            await CreateRunAsync(store, "run-1");
            await store.AppendWorkflowEventsAsync(
                "project-a", "run-1", "client-a", [Event("one", 1)], [],
                TestContext.Current.CancellationToken);
            Assert.NotNull(await store.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken));
            File.Delete(Assert.Single(Directory.GetFiles(
                store.WorkflowCheckpointRoot!,
                "*.json",
                SearchOption.AllDirectories)));
            Volatile.Write(ref armed, 1);

            await store.ReconcileWorkflowCheckpointsAsync(
                TestContext.Current.CancellationToken);

            var current = await store.GetWorkflowRunAsync(
                "project-a", "run-1", TestContext.Current.CancellationToken);
            Assert.Equal(2UL, current!.ActiveCheckpointSequence);
            Assert.True(WorkflowCheckpointStore.HasCanonicalManifest(current));
        }
        finally
        {
            if (store is not null)
                await store.DisposeAsync();
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Returned_manifest_repairs_survive_later_page_bookkeeping_failure()
    {
        var databasePath = DatabasePath("manifest-repair-queue");
        var failRepairBookkeeping = 1;
        var failProjection = 0;
        var projectionAttempts = 0;
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                beforeProjectionQuantum: (_, _, _) =>
                {
                    if (Volatile.Read(ref failProjection) is 1)
                    {
                        Interlocked.Increment(ref projectionAttempts);
                        throw new IOException("repair projection fixture");
                    }
                    return ValueTask.CompletedTask;
                },
                beforeCheckpointReconciliation: (stage, _) =>
                {
                    if (stage is WorkflowCheckpointReconciliationStage.ManifestRepaired &&
                        Interlocked.Exchange(ref failRepairBookkeeping, 0) is 1)
                    {
                        throw new IOException("post-repair bookkeeping fixture");
                    }
                    return ValueTask.CompletedTask;
                });
            foreach (var runId in new[] { "run-1", "run-2" })
            {
                await CreateRunAsync(store, runId);
                await store.AppendWorkflowEventsAsync(
                    "project-a", runId, "client-a", [Event($"{runId}-one", 1)], [],
                    TestContext.Current.CancellationToken);
                Assert.NotNull(await store.GetWorkflowGraphAsync(
                    "project-a", runId, null, 100, null, 100,
                    TestContext.Current.CancellationToken));
            }
            foreach (var checkpoint in Directory.GetFiles(
                         store.WorkflowCheckpointRoot!,
                         "*.json",
                         SearchOption.AllDirectories))
            {
                File.Delete(checkpoint);
            }

            await Assert.ThrowsAsync<IOException>(() =>
                store.ReconcileWorkflowCheckpointsAsync(
                    TestContext.Current.CancellationToken));
            await using (var connection = new DuckDBConnection($"DataSource={databasePath}"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT count(*) FROM workflow_checkpoint_repairs";
                Assert.Equal(
                    2,
                    Convert.ToInt32(
                        await command.ExecuteScalarAsync(
                            TestContext.Current.CancellationToken)));
            }

            Volatile.Write(ref failProjection, 1);
            await store.ReconcileWorkflowCheckpointsAsync(
                TestContext.Current.CancellationToken);
            await WaitUntilAsync(() => Task.FromResult(
                Volatile.Read(ref projectionAttempts) >= 8));
            await using (var pending = new DuckDBConnection($"DataSource={databasePath}"))
            {
                await pending.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = pending.CreateCommand();
                command.CommandText = "SELECT count(*) FROM workflow_checkpoint_repairs";
                Assert.Equal(
                    2,
                    Convert.ToInt32(
                        await command.ExecuteScalarAsync(
                            TestContext.Current.CancellationToken)));
            }

            foreach (var runId in new[] { "run-1", "run-2" })
            {
                await store.AppendWorkflowEventsAsync(
                    "project-a",
                    runId,
                    "client-a",
                    [Event($"{runId}-two", 2)],
                    [],
                    TestContext.Current.CancellationToken);
            }
            await store.ReconcileWorkflowCheckpointsAsync(
                TestContext.Current.CancellationToken);
            await using (var refreshed = new DuckDBConnection($"DataSource={databasePath}"))
            {
                await refreshed.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = refreshed.CreateCommand();
                command.CommandText = """
                                      SELECT min(latest_journal_sequence)
                                      FROM workflow_checkpoint_repairs
                                      """;
                Assert.Equal(
                    2UL,
                    Convert.ToUInt64(
                        await command.ExecuteScalarAsync(
                            TestContext.Current.CancellationToken)));
            }
            Volatile.Write(ref failProjection, 0);
            await store.ReconcileWorkflowCheckpointsAsync(
                TestContext.Current.CancellationToken);
            foreach (var runId in new[] { "run-1", "run-2" })
            {
                var graph = await store.GetWorkflowGraphAsync(
                    "project-a", runId, null, 100, null, 100,
                    TestContext.Current.CancellationToken);
                Assert.Equal(2UL, graph!.JournalSequence);
            }
            await using (var completed = new DuckDBConnection($"DataSource={databasePath}"))
            {
                await completed.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = completed.CreateCommand();
                command.CommandText = "SELECT count(*) FROM workflow_checkpoint_repairs";
                Assert.Equal(
                    0,
                    Convert.ToInt32(
                        await command.ExecuteScalarAsync(
                            TestContext.Current.CancellationToken)));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Terminal_events_latch_across_later_state_regressions_in_the_same_batch()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store, "completed-run");
        await CreateRunAsync(store, "failed-run");

        await store.AppendWorkflowEventsAsync(
            "project-a",
            "completed-run",
            "client-a",
            [
                Event("completed", 1, WorkflowJournalEventKind.RunCompleted),
                Event("attempt-after-completion", 2, WorkflowJournalEventKind.AttemptStarted)
            ],
            [],
            TestContext.Current.CancellationToken);
        await store.AppendWorkflowEventsAsync(
            "project-a",
            "failed-run",
            "client-a",
            [
                Event("failed", 1, WorkflowJournalEventKind.RunCompleted) with
                {
                    DataJson = """{"status":"failed"}"""
                },
                Event("interrupt-after-failure", 2, WorkflowJournalEventKind.TurnInterrupted)
            ],
            [],
            TestContext.Current.CancellationToken);

        var completed = await store.GetWorkflowRunAsync(
            "project-a", "completed-run", TestContext.Current.CancellationToken);
        var failed = await store.GetWorkflowRunAsync(
            "project-a", "failed-run", TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowRunStatus.Completed, completed!.Status);
        Assert.Equal(WorkflowRunStatus.Failed, failed!.Status);
        Assert.Equal(2UL, completed.LatestJournalSequence);
        Assert.Equal(2UL, failed.LatestJournalSequence);
        await Assert.ThrowsAsync<WorkflowEventConflictException>(() =>
            store.AppendWorkflowEventsAsync(
                "project-a",
                "completed-run",
                "client-a",
                [Event("resume", 3, WorkflowJournalEventKind.AttemptStarted)],
                [],
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<WorkflowEventConflictException>(() =>
            store.AppendWorkflowEventsAsync(
                "project-a",
                "failed-run",
                "client-a",
                [Event("interrupt", 3, WorkflowJournalEventKind.TurnInterrupted)],
                [],
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Exact_historical_graph_preserves_terminal_status_before_a_newer_head()
    {
        var databasePath = DatabasePath("exact-terminal-head");
        var blockerEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBlocker = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(runtimeWorkerCount: 1),
                beforeProjectionQuantum: async (key, _, token) =>
                {
                    if (key.RunId == "blocker")
                    {
                        blockerEntered.TrySetResult(true);
                        await releaseBlocker.Task.WaitAsync(token);
                    }
                });
            await CreateRunAsync(store, "blocker");
            await store.AppendWorkflowEventsAsync(
                "project-a", "blocker", "client-a", [Event("blocker", 1)], [],
                TestContext.Current.CancellationToken);
            await blockerEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

            var completedRun = await CreateRunAsync(store, "completed-run");
            var failedRun = await CreateRunAsync(store, "failed-run");
            await store.AppendWorkflowEventsAsync(
                "project-a",
                "completed-run",
                "client-a",
                [
                    Event("completed", 1, WorkflowJournalEventKind.RunCompleted),
                    Event("attempt-after-completion", 2, WorkflowJournalEventKind.AttemptStarted)
                ],
                [],
                TestContext.Current.CancellationToken);
            await store.AppendWorkflowEventsAsync(
                "project-a",
                "failed-run",
                "client-a",
                [
                    Event("failed", 1, WorkflowJournalEventKind.RunCompleted) with
                    {
                        DataJson = """{"status":"failed"}"""
                    },
                    Event("interrupt-after-failure", 2, WorkflowJournalEventKind.TurnInterrupted)
                ],
                [],
                TestContext.Current.CancellationToken);

            var completedHead = store.WaitForWorkflowProjectionAsync(
                new WorkflowProjectionKey(
                    "project-a",
                    "completed-run",
                    completedRun.RunGeneration),
                2,
                TestContext.Current.CancellationToken);
            var failedHead = store.WaitForWorkflowProjectionAsync(
                new WorkflowProjectionKey(
                    "project-a",
                    "failed-run",
                    failedRun.RunGeneration),
                2,
                TestContext.Current.CancellationToken);
            await store.SubmitWorkflowControlAsync(
                "project-a",
                "completed-run",
                WorkflowControlAction.Interrupt,
                "completed-control",
                null,
                s_timestamp.AddSeconds(1),
                TestContext.Current.CancellationToken);
            await store.SubmitWorkflowControlAsync(
                "project-a",
                "failed-run",
                WorkflowControlAction.Interrupt,
                "failed-control",
                null,
                s_timestamp.AddSeconds(2),
                TestContext.Current.CancellationToken);
            releaseBlocker.TrySetResult(true);

            var completed = await completedHead.WaitAsync(
                TestContext.Current.CancellationToken);
            var failed = await failedHead.WaitAsync(
                TestContext.Current.CancellationToken);
            Assert.Equal(2UL, completed.JournalSequence);
            Assert.Equal(WorkflowRunStatus.Completed, completed.Graph.Run.Status);
            Assert.Equal(2UL, failed.JournalSequence);
            Assert.Equal(WorkflowRunStatus.Failed, failed.Graph.Run.Status);
        }
        finally
        {
            releaseBlocker.TrySetResult(true);
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Graph_endpoint_maps_projection_capacity_to_conflict()
    {
        await using var store = new DuckDbStore(
            ":memory:",
            workflowProjectionLimits: new WorkflowProjectionLimits(
                maxRuntimeCacheBytes: 1));
        await CreateRunAsync(store, "run-1");
        var context = EndpointContext();

        var result = await CollectorEndpointExtensions.GetGraphAsync(
            context,
            "run-1",
            store,
            TestContext.Current.CancellationToken);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Fact]
    public async Task Graph_endpoint_maps_runtime_admission_to_service_unavailable()
    {
        var databasePath = DatabasePath("graph-endpoint-capacity");
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(
                    maxRuntimeDemands: 1,
                    runtimeWorkerCount: 1),
                beforeProjectionQuantum: async (key, _, token) =>
                {
                    if (key.RunId == "blocker")
                    {
                        entered.TrySetResult(true);
                        await release.Task.WaitAsync(token);
                    }
                });
            await CreateRunAsync(store, "blocker");
            await store.AppendWorkflowEventsAsync(
                "project-a", "blocker", "client-a", [Event("blocker", 1)], [],
                TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            await CreateRunAsync(store, "target");
            var context = EndpointContext();

            var result = await CollectorEndpointExtensions.GetGraphAsync(
                context,
                "target",
                store,
                TestContext.Current.CancellationToken);
            await result.ExecuteAsync(context);

            Assert.Equal(
                StatusCodes.Status503ServiceUnavailable,
                context.Response.StatusCode);
            release.TrySetResult(true);
        }
        finally
        {
            release.TrySetResult(true);
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Graph_endpoint_maps_generation_retirement_to_not_found()
    {
        var databasePath = DatabasePath("graph-endpoint-gone");
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                beforeProjectionQuantum: async (key, _, token) =>
                {
                    if (key.RunId == "run-1")
                    {
                        entered.TrySetResult(true);
                        await release.Task.WaitAsync(token);
                    }
                });
            await CreateRunAsync(store, "run-1");
            await store.AppendWorkflowEventsAsync(
                "project-a",
                "run-1",
                "client-a",
                [Event("completed", 1, WorkflowJournalEventKind.RunCompleted)],
                [],
                TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            await SetLastActivityAsync(
                databasePath,
                ["run-1"],
                s_timestamp.AddDays(-40));
            var context = EndpointContext();
            var endpoint = CollectorEndpointExtensions.GetGraphAsync(
                context,
                "run-1",
                store,
                TestContext.Current.CancellationToken);
            var deletion = store.DeleteExpiredWorkflowDataBatchAsync(
                s_timestamp.AddDays(-30),
                1,
                TestContext.Current.CancellationToken);
            await WaitUntilAsync(async () =>
                (await store.GetWorkflowRunAsync(
                    "project-a",
                    "run-1",
                    TestContext.Current.CancellationToken)) is null);
            release.TrySetResult(true);

            var result = await endpoint;
            await result.ExecuteAsync(context);
            await deletion;
            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        }
        finally
        {
            release.TrySetResult(true);
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Fire_and_forget_projection_rejection_emits_low_cardinality_metric()
    {
        var databasePath = DatabasePath("projection-observability");
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var measurements = new ConcurrentQueue<(string Outcome, string Reason)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == QylTelemetry.ServiceName &&
                instrument.Name == "qyl.workflow.lifecycle.outcomes")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>(
            (_, _, tags, _) =>
            {
                string? outcome = null;
                string? reason = null;
                foreach (var tag in tags)
                {
                    if (tag.Key == "outcome")
                        outcome = tag.Value as string;
                    else if (tag.Key == "reason")
                        reason = tag.Value as string;
                }
                if (outcome is not null && reason is not null)
                    measurements.Enqueue((outcome, reason));
            });
        listener.Start();
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(
                    maxRuntimeDemands: 1,
                    runtimeWorkerCount: 1),
                beforeProjectionQuantum: async (key, _, token) =>
                {
                    if (key.RunId == "blocker")
                    {
                        entered.TrySetResult(true);
                        await release.Task.WaitAsync(token);
                    }
                });
            await CreateRunAsync(store, "blocker");
            await store.AppendWorkflowEventsAsync(
                "project-a", "blocker", "client-a", [Event("blocker", 1)], [],
                TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            await CreateRunAsync(store, "rejected");
            await store.AppendWorkflowEventsAsync(
                "project-a", "rejected", "client-a", [Event("rejected", 1)], [],
                TestContext.Current.CancellationToken);

            Assert.Contains(
                measurements,
                static measurement =>
                    measurement is ("rejected", "demand_capacity"));
            release.TrySetResult(true);
        }
        finally
        {
            release.TrySetResult(true);
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Projection_runtime_bounds_workers_admission_and_idle_cache_bytes()
    {
        var admissionDatabase = DatabasePath("runtime-admission");
        var evictionDatabase = DatabasePath("runtime-eviction");
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var twoWorkersEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        try
        {
            await using (var store = new DuckDbStore(
                             admissionDatabase,
                             maxConcurrentReads: 1,
                             workflowProjectionLimits: new WorkflowProjectionLimits(
                                 maxRuntimeDemands: 2,
                                 runtimeWorkerCount: 2),
                             beforeProjectionQuantum: async (_, _, token) =>
                             {
                                 if (Interlocked.Increment(ref active) is 2)
                                     twoWorkersEntered.TrySetResult(true);
                                 try
                                 {
                                     await release.Task.WaitAsync(token);
                                 }
                                 finally
                                 {
                                     Interlocked.Decrement(ref active);
                                 }
                             }))
            {
                foreach (var runId in new[] { "run-1", "run-2", "run-3" })
                {
                    await CreateRunAsync(store, runId);
                    await store.AppendWorkflowEventsAsync(
                        "project-a", runId, "client-a", [Event("one", 1)], [],
                        TestContext.Current.CancellationToken);
                }
                await twoWorkersEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
                var snapshot = store.WorkflowProjectionRuntimeSnapshot;
                Assert.Equal(2, snapshot.WorkerCount);
                Assert.Equal(2, snapshot.ActiveWorkers);
                Assert.Equal(2, snapshot.AdmittedDemands);
                await Assert.ThrowsAsync<QylStoreUnavailableException>(() =>
                    store.GetWorkflowGraphAsync(
                        "project-a", "run-3", null, 100, null, 100,
                        TestContext.Current.CancellationToken));
                release.TrySetResult(true);
                await store.GetWorkflowGraphAsync(
                    "project-a", "run-1", null, 100, null, 100,
                    TestContext.Current.CancellationToken);
                await store.GetWorkflowGraphAsync(
                    "project-a", "run-2", null, 100, null, 100,
                    TestContext.Current.CancellationToken);
            }

            const long CacheBudget = 8 * 1024;
            await using (var store = new DuckDbStore(
                             evictionDatabase,
                             maxConcurrentReads: 1,
                             workflowProjectionLimits: new WorkflowProjectionLimits(
                                 maxRuntimeCacheBytes: CacheBudget,
                                 runtimeWorkerCount: 1)))
            {
                for (var index = 1; index <= 4; index++)
                {
                    var runId = $"run-{index}";
                    await CreateRunAsync(store, runId);
                    await store.AppendWorkflowEventsAsync(
                        "project-a", runId, "client-a", [Event("one", 1)], [],
                        TestContext.Current.CancellationToken);
                    await store.GetWorkflowGraphAsync(
                        "project-a", runId, null, 100, null, 100,
                        TestContext.Current.CancellationToken);
                }
                var snapshot = store.WorkflowProjectionRuntimeSnapshot;
                Assert.InRange(snapshot.CachedBytes, 1, CacheBudget);
                Assert.InRange(snapshot.CachedStates, 1, 3);
            }
        }
        finally
        {
            release.TrySetResult(true);
            DeleteDatabase(admissionDatabase);
            DeleteDatabase(evictionDatabase);
        }
    }

    [Fact]
    public async Task Configuration_fingerprint_mismatch_rebuilds_instead_of_poisoning()
    {
        var databasePath = DatabasePath("checkpoint-fingerprint");
        try
        {
            string originalCheckpoint;
            await using (var seed = new DuckDbStore(databasePath, maxConcurrentReads: 1))
            {
                await CreateRunAsync(seed, "run-1");
                await seed.AppendWorkflowEventsAsync(
                    "project-a", "run-1", "client-a", [Event("one", 1)], [],
                    TestContext.Current.CancellationToken);
                await seed.GetWorkflowGraphAsync(
                    "project-a", "run-1", null, 100, null, 100,
                    TestContext.Current.CancellationToken);
                originalCheckpoint = (await seed.GetWorkflowRunAsync(
                    "project-a", "run-1", TestContext.Current.CancellationToken))!
                    .ActiveCheckpointId!;
            }

            await using var changed = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(maxNodes: 19_999));
            var graph = await changed.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken);
            var run = await changed.GetWorkflowRunAsync(
                "project-a", "run-1", TestContext.Current.CancellationToken);
            Assert.Equal(1UL, graph!.JournalSequence);
            Assert.NotEqual(originalCheckpoint, run!.ActiveCheckpointId);
            Assert.Null(run.ProjectionFailureSequence);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Spaced_appends_serialize_full_checkpoints_only_at_geometric_boundaries()
    {
        var databasePath = DatabasePath("geometric-checkpoints");
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(runtimeWorkerCount: 1));
            await CreateRunAsync(store, "run-1");
            for (ulong sequence = 1; sequence <= 16; sequence++)
            {
                await store.AppendWorkflowEventsAsync(
                    "project-a",
                    "run-1",
                    "client-a",
                    [Event($"event-{sequence}", sequence)],
                    [],
                    TestContext.Current.CancellationToken);
                var graph = await store.GetWorkflowGraphAsync(
                    "project-a", "run-1", null, 100, null, 100,
                    TestContext.Current.CancellationToken);
                Assert.Equal(sequence, graph!.JournalSequence);
            }

            var files = Directory.GetFiles(
                store.WorkflowCheckpointRoot!,
                "*.json",
                SearchOption.AllDirectories);
            Assert.Equal(5, files.Length);
            var run = await store.GetWorkflowRunAsync(
                "project-a", "run-1", TestContext.Current.CancellationToken);
            Assert.Equal(16UL, run!.ActiveCheckpointSequence);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Durable_repair_forces_checkpoint_below_next_geometric_boundary()
    {
        const int DurableSequence = 8_192;
        const int RepairSequence = 10_000;
        var databasePath = DatabasePath("forced-repair-checkpoint");
        var firstProjectionEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstProjection = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstProjection = 0;
        try
        {
            await using var store = new DuckDbStore(
                databasePath,
                maxConcurrentReads: 1,
                workflowProjectionLimits: new WorkflowProjectionLimits(
                    runtimeWorkerCount: 1,
                    runtimeEventQuantum: DurableSequence),
                beforeProjectionQuantum: async (_, _, token) =>
                {
                    if (Interlocked.Exchange(ref firstProjection, 1) is 0)
                    {
                        firstProjectionEntered.TrySetResult(true);
                        await releaseFirstProjection.Task.WaitAsync(token);
                    }
                });
            var run = await CreateRunAsync(store, "run-1");
            await store.AppendWorkflowEventsAsync(
                "project-a",
                "run-1",
                "client-a",
                [Event("event-1", 1)],
                [],
                TestContext.Current.CancellationToken);
            await firstProjectionEntered.Task.WaitAsync(
                TestContext.Current.CancellationToken);
            for (var start = 2; start <= DurableSequence; start += 256)
            {
                var count = Math.Min(256, DurableSequence - start + 1);
                await store.AppendWorkflowEventsAsync(
                    "project-a",
                    "run-1",
                    "client-a",
                    Enumerable.Range(start, count)
                        .Select(static sequence =>
                            Event($"event-{sequence}", (ulong)sequence))
                        .ToArray(),
                    [],
                    TestContext.Current.CancellationToken);
            }
            releaseFirstProjection.TrySetResult(true);
            Assert.Equal(
                (ulong)DurableSequence,
                (await store.GetWorkflowGraphAsync(
                    "project-a", "run-1", null, 100, null, 100,
                    TestContext.Current.CancellationToken))!.JournalSequence);
            Assert.Equal(
                (ulong)DurableSequence,
                (await store.GetWorkflowRunAsync(
                    "project-a", "run-1",
                    TestContext.Current.CancellationToken))!.ActiveCheckpointSequence);

            for (var start = DurableSequence + 1;
                 start <= RepairSequence;
                 start += 256)
            {
                var count = Math.Min(256, RepairSequence - start + 1);
                await store.AppendWorkflowEventsAsync(
                    "project-a",
                    "run-1",
                    "client-a",
                    Enumerable.Range(start, count)
                        .Select(static sequence =>
                            Event($"event-{sequence}", (ulong)sequence))
                        .ToArray(),
                    [],
                    TestContext.Current.CancellationToken);
            }
            Assert.Equal(
                (ulong)RepairSequence,
                (await store.GetWorkflowGraphAsync(
                    "project-a", "run-1", null, 100, null, 100,
                    TestContext.Current.CancellationToken))!.JournalSequence);
            Assert.Equal(
                (ulong)DurableSequence,
                (await store.GetWorkflowRunAsync(
                    "project-a", "run-1",
                    TestContext.Current.CancellationToken))!.ActiveCheckpointSequence);

            await using (var connection = new DuckDBConnection($"DataSource={databasePath}"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                                      INSERT INTO workflow_checkpoint_repairs (
                                          project_id,
                                          run_id,
                                          run_generation,
                                          latest_journal_sequence)
                                      VALUES ('project-a', 'run-1', $1, $2)
                                      """;
                command.Parameters.Add(new DuckDBParameter
                {
                    Value = run.RunGeneration
                });
                command.Parameters.Add(new DuckDBParameter
                {
                    Value = RepairSequence
                });
                await command.ExecuteNonQueryAsync(
                    TestContext.Current.CancellationToken);
            }

            await store.ReconcileWorkflowCheckpointsAsync(
                TestContext.Current.CancellationToken);
            await WaitUntilAsync(async () =>
            {
                var current = await store.GetWorkflowRunAsync(
                    "project-a",
                    "run-1",
                    TestContext.Current.CancellationToken);
                if (current?.ActiveCheckpointSequence != RepairSequence ||
                    store.WorkflowProjectionRuntimeSnapshot.AdmittedDemands is not 0)
                {
                    return false;
                }
                await using var connection =
                    new DuckDBConnection($"DataSource={databasePath}");
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT count(*) FROM workflow_checkpoint_repairs";
                return Convert.ToInt32(await command.ExecuteScalarAsync(
                    TestContext.Current.CancellationToken)) is 0;
            });
            await store.ReconcileWorkflowCheckpointsAsync(
                TestContext.Current.CancellationToken);
            Assert.Equal(0, store.WorkflowProjectionRuntimeSnapshot.AdmittedDemands);
        }
        finally
        {
            releaseFirstProjection.TrySetResult(true);
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Storage_metrics_include_live_and_temporary_checkpoint_sidecars()
    {
        var databasePath = DatabasePath("sidecar-metrics");
        try
        {
            await using var store = new DuckDbStore(databasePath, maxConcurrentReads: 1);
            await CreateRunAsync(store, "run-1");
            await store.AppendWorkflowEventsAsync(
                "project-a", "run-1", "client-a", [Event("one", 1)], [],
                TestContext.Current.CancellationToken);
            await store.GetWorkflowGraphAsync(
                "project-a", "run-1", null, 100, null, 100,
                TestContext.Current.CancellationToken);
            var temporaryDirectory = Path.Combine(
                store.WorkflowCheckpointRoot!,
                "pending",
                "generation");
            Directory.CreateDirectory(temporaryDirectory);
            var temporary = Path.Combine(temporaryDirectory, ".pending.tmp");
            await File.WriteAllBytesAsync(
                temporary,
                RandomNumberGenerator.GetBytes(257),
                TestContext.Current.CancellationToken);
            await store.ReconcileWorkflowCheckpointsAsync(
                TestContext.Current.CancellationToken);

            var metrics = store.GetStorageFileMetrics();
            Assert.True(metrics.DatabaseFileSizeBytes > 0);
            Assert.True(metrics.LiveCheckpointBytes > 0);
            Assert.True(metrics.TemporaryOrOrphanCheckpointBytes >= 257);
            Assert.Equal(
                metrics.DatabaseFileSizeBytes +
                metrics.WalFileSizeBytes +
                metrics.LiveCheckpointBytes +
                metrics.TemporaryOrOrphanCheckpointBytes,
                metrics.ManagedStorageBytes);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    private static async Task AddLegacyRunsAsync(string databasePath, int count)
    {
        await using var connection = new DuckDBConnection($"DataSource={databasePath}");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        for (var index = 0; index < count; index++)
        {
            await using var run = connection.CreateCommand();
            run.CommandText = """
                              INSERT INTO workflow_runs (
                                  project_id,
                                  run_id,
                                  thread_id,
                                  title,
                                  status,
                                  started_at,
                                  ended_at,
                                  latest_journal_sequence,
                                  active_attempt_id,
                                  metadata_json,
                                  created_at,
                                  updated_at)
                              VALUES (
                                  'project-a',
                                  $1,
                                  NULL,
                                  NULL,
                                  'active',
                                  $2,
                                  NULL,
                                  0,
                                  NULL,
                                  NULL,
                                  $2,
                                  $2)
                              """;
            run.Parameters.Add(new DuckDBParameter { Value = $"legacy-extra-{index:D4}" });
            run.Parameters.Add(new DuckDBParameter { Value = s_timestamp.UtcDateTime });
            await run.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task SeedLegacyWorkflowDatabaseAsync(string databasePath)
    {
        await using var connection = new DuckDBConnection($"DataSource={databasePath}");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using (var schema = connection.CreateCommand())
        {
            schema.CommandText = string.Concat(
                """
                CREATE TABLE workflow_runs (
                    project_id VARCHAR NOT NULL,
                    run_id VARCHAR NOT NULL,
                    thread_id VARCHAR,
                    title VARCHAR,
                    status VARCHAR NOT NULL,
                    started_at TIMESTAMPTZ NOT NULL,
                    ended_at TIMESTAMPTZ,
                    latest_journal_sequence UBIGINT NOT NULL,
                    active_attempt_id VARCHAR,
                    metadata_json JSON,
                    created_at TIMESTAMPTZ NOT NULL,
                    updated_at TIMESTAMPTZ NOT NULL,
                    PRIMARY KEY (project_id, run_id)
                );
                CREATE TABLE workflow_projection_nodes (node_id VARCHAR);
                CREATE TABLE workflow_projection_edges (edge_id VARCHAR);
                CREATE TABLE workflow_projection_state (run_id VARCHAR);
                """,
                "\n",
                WorkflowEventDbRow.CreateTableDdl,
                "\n",
                WorkflowCommandDbRow.CreateTableDdl);
            await schema.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var legacyActivity = s_timestamp.AddDays(-40);
        await using (var run = connection.CreateCommand())
        {
            run.CommandText = """
                              INSERT INTO workflow_runs (
                                  project_id,
                                  run_id,
                                  thread_id,
                                  title,
                                  status,
                                  started_at,
                                  ended_at,
                                  latest_journal_sequence,
                                  active_attempt_id,
                                  metadata_json,
                                  created_at,
                                  updated_at)
                              VALUES (
                                  'project-a',
                                  'run-1',
                                  'thread-1',
                                  'Legacy fixture',
                                  'active',
                                  $1,
                                  NULL,
                                  2,
                                  NULL,
                                  NULL,
                                  $2,
                                  $2)
                              """;
            run.Parameters.Add(new DuckDBParameter
            {
                Value = s_timestamp.UtcDateTime
            });
            run.Parameters.Add(new DuckDBParameter
            {
                Value = legacyActivity.UtcDateTime
            });
            await run.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        foreach (var (journalSequence, sourceSequence, eventId) in new[]
                 {
                     (1UL, 1UL, "legacy-one"),
                     (2UL, 3UL, "legacy-three")
                 })
        {
            await using var workflowEvent = connection.CreateCommand();
            workflowEvent.CommandText = WorkflowEventDbRow.BuildMultiRowInsertSql(1);
            WorkflowEventDbRow.AddParameters(
                workflowEvent,
                new WorkflowEventDbRow
                {
                    ProjectId = "project-a",
                    RunId = "run-1",
                    JournalSequence = journalSequence,
                    EventId = eventId,
                    ClientId = "client-a",
                    SourceSequence = sourceSequence,
                    EventTime = s_timestamp.AddMilliseconds(journalSequence),
                    Kind = "content_captured",
                    ThreadId = "thread-1",
                    TurnId = null,
                    AttemptId = null,
                    AgentId = null,
                    ParentAgentId = null,
                    ReceiverAgentId = null,
                    ToolCallId = null,
                    ContentRefsJson = "[]",
                    DataJson = null
                });
            await workflowEvent.ExecuteNonQueryAsync(
                TestContext.Current.CancellationToken);
        }

        await using var workflowCommand = connection.CreateCommand();
        workflowCommand.CommandText = WorkflowCommandDbRow.BuildMultiRowInsertSql(1);
        WorkflowCommandDbRow.AddParameters(
            workflowCommand,
            new WorkflowCommandDbRow
            {
                ProjectId = "project-a",
                RunId = "run-1",
                CommandId = "legacy-command",
                CommandSequence = 4,
                Action = "interrupt",
                Status = "requested",
                IdempotencyKey = "legacy-command",
                Input = null,
                RequestedAt = legacyActivity,
                UpdatedAt = legacyActivity,
                Error = null
            });
        await workflowCommand.ExecuteNonQueryAsync(
            TestContext.Current.CancellationToken);
    }

    private static DefaultHttpContext EndpointContext()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider()
        };
        context.Request.Headers["X-Qyl-Project"] = "project-a";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static Task<WorkflowRunStorageRow> CreateRunAsync(
        DuckDbStore store,
        string runId,
        CancellationToken ct = default) =>
        store.CreateWorkflowRunAsync(
            new WorkflowRunStorageRow(
                "project-a",
                runId,
                "thread-1",
                "Lifecycle fixture",
                WorkflowRunStatus.Active,
                s_timestamp,
                null,
                0,
                null,
                null),
            ct == default ? TestContext.Current.CancellationToken : ct);

    private static WorkflowEventWrite Event(
        string eventId,
        ulong sourceSequence,
        WorkflowJournalEventKind kind = WorkflowJournalEventKind.ContentCaptured) =>
        new(
            eventId,
            sourceSequence,
            s_timestamp.AddMilliseconds(sourceSequence),
            kind,
            "thread-1",
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            null);

    private static async Task SetLastActivityAsync(
        string databasePath,
        IReadOnlyList<string> runIds,
        DateTimeOffset lastActivity)
    {
        await using var connection = new DuckDBConnection($"DataSource={databasePath}");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        foreach (var runId in runIds)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                                  UPDATE workflow_runs
                                  SET last_activity_at = $1
                                  WHERE project_id = 'project-a' AND run_id = $2
                                  """;
            command.Parameters.Add(new DuckDBParameter
            {
                Value = lastActivity.UtcDateTime
            });
            command.Parameters.Add(new DuckDBParameter { Value = runId });
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await condition())
                return;
            await Task.Delay(
                TimeSpan.FromMilliseconds(10),
                TestContext.Current.CancellationToken);
        }
        throw new TimeoutException("Condition did not become true.");
    }

    private static string SourcePath(
        [System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        path;

    private static string DatabasePath(string testName) =>
        Path.Combine(
            Path.GetTempPath(),
            $"qyl-workflow-lifecycle-{testName}-{Guid.NewGuid():N}.duckdb");

    private static void DeleteDatabase(string databasePath)
    {
        File.Delete(databasePath);
        File.Delete($"{databasePath}.wal");
        var checkpoints = $"{databasePath}.workflow-checkpoints";
        if (Directory.Exists(checkpoints))
            Directory.Delete(checkpoints, recursive: true);
    }
}
