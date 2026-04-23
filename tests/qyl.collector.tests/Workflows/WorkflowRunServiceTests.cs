// Copyright (c) 2025-2026 ancplua

using DuckDB.NET.Data;
using Qyl.Collector.Workflows;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests.Workflows;

public sealed class WorkflowRunServiceTests : IAsyncDisposable
{
    private readonly string _dbPath = Path.ChangeExtension(Path.GetTempFileName(), ".duckdb");
    private readonly DuckDbStore _store;
    private readonly WorkflowRunService _service;

    public WorkflowRunServiceTests()
    {
        _store = new DuckDbStore(_dbPath);
        _service = new WorkflowRunService(_store);
    }

    [Fact]
    public async Task Control_plane_reads_events_checkpoints_and_mutates_gated_nodes()
    {
        var ct = TestContext.Current.CancellationToken;
        var createdAt = new DateTime(2026, 4, 23, 10, 0, 0, DateTimeKind.Utc);

        await SeedWorkflowAsync(createdAt, ct);

        var runs = await _service.ListRunsAsync(
            projectId: "project-a",
            workflowId: "loom.autofix",
            status: "pending",
            startTime: createdAt.AddMinutes(-1),
            endTime: createdAt.AddMinutes(1),
            limit: 1,
            cursor: null,
            ct: ct);

        runs.Items.Should().ContainSingle();
        runs.Items[0].Id.Should().Be("run-1");
        runs.HasMore.Should().BeFalse();

        var events = await _service.GetRunEventsAsync("run-1", afterSequence: 1, limit: 10, ct);
        events.Should().ContainSingle();
        events[0].EventName.Should().Be("node.awaiting_approval");

        var checkpoints = await _service.GetRunCheckpointsAsync("run-1", ct);
        checkpoints.Should().ContainSingle();
        checkpoints[0].StateJson.Should().Be("""{"phase":"approval"}""");

        var approved = await _service.ApproveNodeAsync("run-1", "approval", ct);
        approved.Should().NotBeNull();
        approved.Status.Should().Be("running");

        var resumed = await _service.ResumeRunAsync("run-1", ct);
        resumed.Should().NotBeNull();
        resumed.Status.Should().Be("running");

        var cancelled = await _service.CancelRunAsync("run-1", ct);
        cancelled.Should().NotBeNull();
        cancelled.Status.Should().Be("cancelled");
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private async Task SeedWorkflowAsync(DateTime createdAt, CancellationToken ct)
    {
        await _store.ExecuteWriteAsync(async (connection, ct) =>
        {
            await using (var run = connection.CreateCommand())
            {
                run.CommandText = """
                                  INSERT INTO workflow_runs
                                      (id, workflow_id, workflow_version, project_id, trigger_type,
                                       trigger_source, input_json, output_json, status, error_message,
                                       parent_run_id, correlation_id, started_at, completed_at,
                                       duration_ms, created_at)
                                  VALUES
                                      ('run-1', 'loom.autofix', 1, 'project-a', 'manual',
                                       'user', '{}', NULL, 'pending', NULL,
                                       NULL, 'trace-1', NULL, NULL,
                                       NULL, $1)
                                  """;
                run.Parameters.Add(new DuckDBParameter { Value = createdAt });
                await run.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await using (var node = connection.CreateCommand())
            {
                node.CommandText = """
                                   INSERT INTO workflow_nodes
                                       (id, run_id, node_id, node_type, node_name, attempt,
                                        input_json, output_json, status, error_message,
                                        retry_count, max_retries, timeout_ms,
                                        started_at, completed_at, duration_ms, created_at)
                                   VALUES
                                       ('node-1', 'run-1', 'approval', 'approval', 'Human approval', 1,
                                        '{}', NULL, 'awaiting_approval', NULL,
                                        0, 3, NULL,
                                        NULL, NULL, NULL, $1)
                                   """;
                node.Parameters.Add(new DuckDBParameter { Value = createdAt });
                await node.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await using (var firstEvent = connection.CreateCommand())
            {
                firstEvent.CommandText = """
                                         INSERT INTO workflow_events
                                             (id, run_id, node_id, event_type, event_name,
                                              payload_json, sequence_number, source, correlation_id, timestamp)
                                         VALUES
                                             ('evt-1', 'run-1', NULL, 'run', 'run.started',
                                              '{}', 1, 'test', 'trace-1', $1)
                                         """;
                firstEvent.Parameters.Add(new DuckDBParameter { Value = createdAt });
                await firstEvent.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await using (var secondEvent = connection.CreateCommand())
            {
                secondEvent.CommandText = """
                                          INSERT INTO workflow_events
                                              (id, run_id, node_id, event_type, event_name,
                                               payload_json, sequence_number, source, correlation_id, timestamp)
                                          VALUES
                                              ('evt-2', 'run-1', 'approval', 'node', 'node.awaiting_approval',
                                               '{}', 2, 'test', 'trace-1', $1)
                                          """;
                secondEvent.Parameters.Add(new DuckDBParameter { Value = createdAt.AddSeconds(1) });
                await secondEvent.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await using var checkpoint = connection.CreateCommand();
            checkpoint.CommandText = """
                                     INSERT INTO workflow_checkpoints
                                         (id, run_id, node_id, checkpoint_type, state_json,
                                          sequence_number, created_at)
                                     VALUES
                                         ('chk-1', 'run-1', 'approval', 'node',
                                          '{"phase":"approval"}', 2, $1)
                                     """;
            checkpoint.Parameters.Add(new DuckDBParameter { Value = createdAt.AddSeconds(2) });
            await checkpoint.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);
    }
}
