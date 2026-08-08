using DuckDB.NET.Data;

namespace Qyl.Collector.Storage;

internal sealed partial class DuckDbStore
{
    public Task<int> DeleteExpiredLogsBatchAsync(
        ulong cutoffUnixNano,
        int batchSize,
        CancellationToken ct = default)
    {
        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be positive.");
        return ExecuteMaintenanceWriteAsync(
            (con, token) => DeleteExpiredLogsBatchInternalAsync(con, cutoffUnixNano, batchSize, token),
            ct);
    }

    public Task<int> DeleteExpiredSpansBatchAsync(
        ulong cutoffUnixNano,
        int batchSize,
        CancellationToken ct = default)
    {
        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be positive.");
        return ExecuteMaintenanceWriteAsync(
            (con, token) => DeleteExpiredSpansBatchInternalAsync(con, cutoffUnixNano, batchSize, token),
            ct);
    }

    public Task CheckpointAsync(CancellationToken ct = default) =>
        ExecuteMaintenanceWriteAsync(static async (con, token) =>
        {
            await using var command = con.CreateCommand();
            command.CommandText = "CHECKPOINT";
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            return 0;
        }, ct);

    public async Task<WorkflowRetentionResult> DeleteExpiredWorkflowDataBatchAsync(
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken ct = default)
    {
        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be positive.");
        await _checkpointManifestMutationGate.WaitAsync(ct).ConfigureAwait(false);
        (
            WorkflowRetentionResult Result,
            IReadOnlyList<WorkflowProjectionKey> Runs,
            WorkflowCheckpointManifestMutation? Mutation) deletion;
        try
        {
            deletion = await ExecuteMaintenanceWriteAsync(async (con, token) =>
            {
                await using var transaction = await con
                    .BeginTransactionAsync(token)
                    .ConfigureAwait(false);
                var expiredRunKeys = new List<(string ProjectId, string RunId)>();
                var expiredRuns = new List<WorkflowRunStorageRow>();
                await using (var select = con.CreateCommand())
                {
                    select.Transaction = transaction;
                    select.CommandText = """
                                          SELECT workflow_runs.project_id, workflow_runs.run_id
                                          FROM workflow_runs
                                          JOIN workflow_run_summaries AS summary
                                            ON summary.project_id = workflow_runs.project_id
                                           AND summary.run_id = workflow_runs.run_id
                                          WHERE summary.status IN ('completed', 'failed')
                                            AND workflow_runs.deleted_at IS NULL
                                            AND workflow_runs.last_activity_at < $1
                                          ORDER BY workflow_runs.last_activity_at,
                                                   workflow_runs.project_id,
                                                   workflow_runs.run_id
                                          LIMIT $2
                                          """;
                    AddParameters(select, cutoff.UtcDateTime, batchSize);
                    await using var reader = await select
                        .ExecuteReaderAsync(token)
                        .ConfigureAwait(false);
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                        expiredRunKeys.Add((reader.GetString(0), reader.GetString(1)));
                }
                foreach (var key in expiredRunKeys)
                {
                    if (ReadWorkflowRun(con, key.ProjectId, key.RunId, transaction) is { } run)
                        expiredRuns.Add(run);
                }

                var epoch = expiredRuns.Count is 0
                    ? 0
                    : await AdvanceWorkflowCheckpointEpochAsync(
                            con,
                            transaction,
                            token)
                        .ConfigureAwait(false);
                foreach (var run in expiredRuns)
                {
                    // Retention may retire disposable projections, but the
                    // append-only journal remains the authoritative history.
                    // A durable tombstone makes deletion observable and blocks
                    // every later append or publication for this run id.
                    await using var delete = con.CreateCommand();
                    delete.Transaction = transaction;
                    delete.CommandText = $"""
                                         UPDATE {WorkflowRunDbRow.TableName}
                                         SET {WorkflowRunDbRow.DeletedAtColumnName} = current_timestamp,
                                             {WorkflowRunDbRow.ActiveCheckpointSequenceColumnName} = 0,
                                             {WorkflowRunDbRow.ActiveCheckpointIdColumnName} = NULL,
                                             {WorkflowRunDbRow.ActiveCheckpointStorageKeyColumnName} = NULL,
                                             {WorkflowRunDbRow.ActiveCheckpointInputHashColumnName} = NULL,
                                             {WorkflowRunDbRow.ActiveCheckpointSemanticFingerprintColumnName} = NULL,
                                             {WorkflowRunDbRow.ActiveCheckpointConfigurationFingerprintColumnName} = NULL,
                                             {WorkflowRunDbRow.ActiveCheckpointFormatVersionColumnName} = NULL,
                                             {WorkflowRunDbRow.ActiveCheckpointByteLengthColumnName} = NULL,
                                             {WorkflowRunDbRow.ActiveCheckpointCreatedAtColumnName} = NULL,
                                             {WorkflowRunDbRow.CheckpointManifestEpochColumnName} = $4,
                                             {WorkflowRunDbRow.UpdatedAtColumnName} = current_timestamp
                                         WHERE {WorkflowRunDbRow.ProjectIdColumnName} = $1
                                           AND {WorkflowRunDbRow.RunIdColumnName} = $2
                                           AND {WorkflowRunDbRow.RunGenerationColumnName} = $3
                                           AND {WorkflowRunDbRow.DeletedAtColumnName} IS NULL;
                                         """;
                    WorkflowRunDbRow.AddRetentionTombstoneParameters(
                        delete,
                        run.ProjectId,
                        run.RunId,
                        run.RunGeneration,
                        epoch);
                    await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                var contentCount = 0;
                await using (var content = con.CreateCommand())
                {
                    content.Transaction = transaction;
                    content.CommandText = """
                                          DELETE FROM workflow_content
                                          WHERE (project_id, content_ref) IN (
                                              SELECT candidate.project_id, candidate.content_ref
                                              FROM workflow_content AS candidate
                                              WHERE candidate.created_at < $1
                                                AND NOT EXISTS (
                                                    SELECT 1
                                                    FROM workflow_content_refs AS reference
                                                    WHERE reference.project_id = candidate.project_id
                                                      AND reference.content_ref = candidate.content_ref
                                                )
                                              ORDER BY candidate.created_at,
                                                       candidate.project_id,
                                                       candidate.content_ref
                                              LIMIT $2
                                          )
                                          RETURNING content_ref
                                          """;
                    AddParameters(content, cutoff.UtcDateTime, batchSize);
                    await using var reader = await content
                        .ExecuteReaderAsync(token)
                        .ConfigureAwait(false);
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                        contentCount++;
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);
                var keys = expiredRuns
                    .Select(static run => new WorkflowProjectionKey(
                        run.ProjectId,
                        run.RunId,
                        run.RunGeneration))
                    .ToArray();
                var deltas = expiredRuns
                    .Where(static run =>
                        run.ActiveCheckpointStorageKey is not null &&
                        WorkflowCheckpointStore.HasCanonicalManifest(run))
                    .Select((run, ordinal) => new WorkflowCheckpointIdentityDelta(
                        epoch,
                        ordinal,
                        run.ActiveCheckpointStorageKey!,
                        Active: false))
                    .ToArray();
                return (
                    Result: new WorkflowRetentionResult(
                        expiredRuns.Count,
                        0,
                        0,
                        contentCount),
                    Runs: (IReadOnlyList<WorkflowProjectionKey>)keys,
                    Mutation: deltas.Length is 0
                        ? null
                        : new WorkflowCheckpointManifestMutation(epoch, deltas));
            }, ct).ConfigureAwait(false);
            if (deletion.Mutation is not null)
            {
                await _workflowCheckpointStore.ApplyManifestMutationAsync(
                        deletion.Mutation,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            _checkpointManifestMutationGate.Release();
        }

        foreach (var run in deletion.Runs)
        {
            await _workflowProjectionRuntime.RetireAsync(run).ConfigureAwait(false);
            using var cleanup = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            cleanup.CancelAfter(TimeSpan.FromSeconds(30));
            await _workflowCheckpointStore
                .RetireGenerationAsync(run, cleanup.Token)
                .ConfigureAwait(false);
        }

        return deletion.Result;
    }

    public StorageFileMetrics GetStorageFileMetrics()
    {
        ThrowIfDisposed();
        if (_isInMemory)
            return new StorageFileMetrics(0, 0, 0, 0, 0, long.MaxValue);

        var databaseBytes = File.Exists(_databasePath) ? new FileInfo(_databasePath).Length : 0;
        var walPath = $"{_databasePath}.wal";
        var walBytes = File.Exists(walPath) ? new FileInfo(walPath).Length : 0;
        var sidecars = _workflowCheckpointStore.Metrics;
        var managedBytes = checked(databaseBytes + walBytes + sidecars.TotalBytes);
        var databaseDirectory = Path.GetDirectoryName(_databasePath)!;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var drive = DriveInfo.GetDrives()
            .Where(static candidate => candidate.IsReady)
            .Where(candidate => IsWithinDrive(databaseDirectory, candidate.RootDirectory.FullName, comparison))
            .OrderByDescending(static candidate => candidate.RootDirectory.FullName.Length)
            .First();

        return new StorageFileMetrics(
            databaseBytes,
            walBytes,
            sidecars.LiveBytes,
            sidecars.TemporaryOrOrphanBytes,
            managedBytes,
            drive.AvailableFreeSpace);
    }

    private static async ValueTask<int> DeleteExpiredLogsBatchInternalAsync(
        DuckDBConnection con,
        ulong cutoffUnixNano,
        int batchSize,
        CancellationToken ct)
    {
        await using var transaction = await con.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              DELETE FROM logs
                              WHERE (project_id, log_id) IN (
                                  SELECT project_id, log_id
                                  FROM logs
                                  WHERE COALESCE(NULLIF(time_unix_nano, 0), observed_time_unix_nano, 0) < $1
                                  ORDER BY COALESCE(NULLIF(time_unix_nano, 0), observed_time_unix_nano, 0), project_id, log_id
                                  LIMIT $2
                              )
                              RETURNING log_id
                              """;
        command.Parameters.Add(new DuckDBParameter { Value = (decimal)cutoffUnixNano });
        command.Parameters.Add(new DuckDBParameter { Value = batchSize });

        var deleted = 0;
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                deleted++;
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return deleted;
    }

    private static async ValueTask<int> DeleteExpiredSpansBatchInternalAsync(
        DuckDBConnection con,
        ulong cutoffUnixNano,
        int batchSize,
        CancellationToken ct)
    {
        await using var transaction = await con.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              DELETE FROM spans
                              WHERE (project_id, trace_id, span_id) IN (
                                  SELECT candidate.project_id, candidate.trace_id, candidate.span_id
                                  FROM spans AS candidate
                                  WHERE candidate.end_time_unix_nano < $1
                                    AND NOT EXISTS (
                                        SELECT 1
                                        FROM spans AS child
                                        WHERE child.project_id = candidate.project_id
                                          AND child.trace_id = candidate.trace_id
                                          AND child.parent_span_id = candidate.span_id
                                    )
                                    AND NOT EXISTS (
                                        SELECT 1
                                        FROM logs AS child_log
                                        WHERE child_log.project_id = candidate.project_id
                                          AND (
                                              child_log.trace_id = candidate.trace_id
                                              OR (child_log.trace_id IS NULL AND child_log.span_id = candidate.span_id)
                                          )
                                    )
                                  ORDER BY candidate.end_time_unix_nano, candidate.project_id,
                                           candidate.trace_id, candidate.span_id
                                  LIMIT $2
                              )
                              RETURNING span_id
                              """;
        command.Parameters.Add(new DuckDBParameter { Value = (decimal)cutoffUnixNano });
        command.Parameters.Add(new DuckDBParameter { Value = batchSize });

        var deleted = 0;
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                deleted++;
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return deleted;
    }

    private static bool IsWithinDrive(string path, string root, StringComparison comparison)
    {
        if (path.Equals(Path.TrimEndingDirectorySeparator(root), comparison))
            return true;

        var rootWithSeparator = Path.EndsInDirectorySeparator(root)
            ? root
            : root + Path.DirectorySeparatorChar;
        return path.StartsWith(rootWithSeparator, comparison);
    }
}
