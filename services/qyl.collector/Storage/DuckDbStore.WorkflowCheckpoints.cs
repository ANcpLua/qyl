using DuckDB.NET.Data;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Telemetry;
using Qyl.Collector.Workflow;

namespace Qyl.Collector.Storage;

internal enum WorkflowCheckpointReconciliationPhase
{
    Manifests,
    Sweep
}

internal enum WorkflowCheckpointReconciliationStage
{
    ManifestValidated,
    ManifestRepaired,
    SweepMetadataRead,
    SweepPrepared,
    SweepClaimed
}

internal readonly record struct WorkflowCheckpointRepair(
    WorkflowProjectionKey Key,
    ulong LatestJournalSequence);

internal sealed partial class DuckDbStore
{
    // Null means the run no longer exists. Retirement and generation rotation
    // are resolved inside the runtime, never surfaced here.
    private async Task<WorkflowProjectionCheckpoint?> AwaitWorkflowProjectionAsync(
        WorkflowRunStorageRow observed,
        CancellationToken ct)
    {
        var key = new WorkflowProjectionKey(
            observed.ProjectId,
            observed.RunId,
            observed.RunGeneration);
        try
        {
            ThrowPersistedWorkflowProjectionFailure(observed, observed.LatestJournalSequence);
            return await _workflowProjectionRuntime.WaitForAsync(
                key,
                observed.LatestJournalSequence,
                ct).ConfigureAwait(false);
        }
        catch (QylStoreUnavailableException error)
        {
            throw new WorkflowProjectionUnavailableException(
                observed.RunGeneration,
                observed.LatestJournalSequence,
                retryAfterMilliseconds: 1000,
                rebuilding: _activatedCheckpointRepairs.ContainsKey(key),
                error);
        }
        catch (WorkflowProjectionLimitExceededException error)
        {
            throw new WorkflowProjectionCorruptException(
                observed.RunGeneration,
                "projection_limits_exceeded",
                error);
        }
        catch (Exception error) when (
            error is WorkflowCheckpointIncompatibleException
                or InvalidDataException
                or JsonException)
        {
            throw new WorkflowProjectionCorruptException(
                observed.RunGeneration,
                "projection_recovery_failed",
                error);
        }
    }

    internal async Task<bool> IsWorkflowProjectionGenerationCurrentAsync(
        WorkflowProjectionKey key,
        CancellationToken ct) =>
        await GetWorkflowRunAsync(key.ProjectId, key.RunId, ct).ConfigureAwait(false)
            is { } run &&
        run.RunGeneration == key.RunGeneration;

    // A projection key stops being current either because the run was deleted
    // (Gone) or because its generation rotated (Rotated, carrying the live key).
    // An unchanged generation can never be a rotation, so it resolves to Gone
    // rather than handing waiters back to the key they already hold.
    private static WorkflowProjectionStep Missing(
        WorkflowRunStorageRow? current,
        WorkflowProjectionKey key) =>
        current is null || current.RunGeneration == key.RunGeneration
            ? WorkflowProjectionStep.Gone.Instance
            : new WorkflowProjectionStep.Rotated(
                new WorkflowProjectionKey(
                    current.ProjectId,
                    current.RunId,
                    current.RunGeneration));

    internal async Task<WorkflowProjectionStep> ProjectWorkflowQuantumAsync(
        WorkflowProjectionKey key,
        WorkflowProjectionState? state,
        ulong targetSequence,
        ulong forcePersistThroughSequence,
        CancellationToken ct)
    {
        var run = await GetWorkflowRunAsync(key.ProjectId, key.RunId, ct).ConfigureAwait(false);
        if (run is null || run.RunGeneration != key.RunGeneration)
            return Missing(run, key);
        ThrowPersistedWorkflowProjectionFailure(run, targetSequence);

        WorkflowProjectionCheckpoint? prior = state?.Checkpoint;
        var durableSequence = state?.DurableSequence ?? 0;
        var durableCheckpointId = state?.DurableCheckpointId;
        if (prior is null && run.ActiveCheckpointId is not null)
        {
            try
            {
                var durable = await _workflowCheckpointStore.ReadAsync(run, ct).ConfigureAwait(false);
                durableSequence = durable.JournalSequence;
                durableCheckpointId = run.ActiveCheckpointId;
                if (durable.JournalSequence <= targetSequence)
                    prior = durable;
            }
            catch (Exception error) when (
                error is WorkflowCheckpointIncompatibleException
                    or InvalidDataException
                    or JsonException
                    or FileNotFoundException)
            {
                durableSequence = run.ActiveCheckpointSequence > targetSequence
                    ? run.ActiveCheckpointSequence
                    : 0;
                durableCheckpointId = null;
            }
        }

        var afterSequence = prior?.JournalSequence ?? 0;
        if (afterSequence > targetSequence)
            throw new InvalidDataException(
                "Workflow projection state is ahead of its requested head.");
        if (afterSequence == targetSequence &&
            prior is not null &&
            durableSequence >= forcePersistThroughSequence)
        {
            return new WorkflowProjectionStep.Advanced(
                state ?? new WorkflowProjectionState(
                    prior,
                    durableSequence,
                    durableCheckpointId,
                    WorkflowProjectionMemory.Estimate(prior)));
        }

        try
        {
            WorkflowProjectionCheckpoint checkpoint;
            WorkflowRunStorageRow projectionRun;
            WorkflowProjectionState nextState;
            if (afterSequence == targetSequence && prior is not null)
            {
                checkpoint = prior;
                projectionRun = run;
                nextState = state ?? new WorkflowProjectionState(
                    prior,
                    durableSequence,
                    durableCheckpointId,
                    WorkflowProjectionMemory.Estimate(prior));
            }
            else
            {
                if (_beforeProjectionQuantum is not null)
                {
                    await _beforeProjectionQuantum(key, targetSequence, ct)
                        .ConfigureAwait(false);
                }
                var input = await ReadWorkflowProjectionQuantumAsync(
                    key,
                    afterSequence,
                    targetSequence,
                    prior,
                    validateFullReplay: prior is null,
                    ct).ConfigureAwait(false);
                if (input is null)
                {
                    return Missing(
                        await GetWorkflowRunAsync(key.ProjectId, key.RunId, ct)
                            .ConfigureAwait(false),
                        key);
                }
                checkpoint = BuildWorkflowCheckpoint(input, prior);
                projectionRun = input.Run;
                nextState = new WorkflowProjectionState(
                    checkpoint,
                    durableSequence,
                    durableCheckpointId,
                    WorkflowProjectionMemory.Estimate(checkpoint));
            }

            if (ShouldPersistWorkflowCheckpoint(
                    checkpoint.JournalSequence,
                    durableSequence,
                    forcePersistThroughSequence,
                    projectionRun))
            {
                var blob = await _workflowCheckpointStore
                    .WriteAsync(checkpoint, ct)
                    .ConfigureAwait(false);
                var published = false;
                try
                {
                    published = await PublishWorkflowCheckpointAsync(
                        projectionRun,
                        checkpoint,
                        blob,
                        ct).ConfigureAwait(false);
                }
                finally
                {
                    await _workflowCheckpointStore.CompleteCandidateAsync(
                            projectionRun,
                            blob,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                WorkflowLifecycleLog.CheckpointPublication(
                    _logger,
                    published ? "won" : "lost");
                if (!published)
                {
                    var current = await GetWorkflowRunAsync(
                        key.ProjectId,
                        key.RunId,
                        ct).ConfigureAwait(false);
                    if (current is null || current.RunGeneration != key.RunGeneration)
                        return Missing(current, key);
                    throw new IOException(
                        "Workflow checkpoint publication lost its generation-aware compare-and-swap.");
                }
                nextState = nextState with
                {
                    DurableSequence = checkpoint.JournalSequence,
                    DurableCheckpointId = blob.CheckpointId
                };
            }
            else if (projectionRun.ProjectionFailureSequence.HasValue)
            {
                await ClearWorkflowProjectionFailureAsync(
                    key,
                    checkpoint.JournalSequence,
                    ct).ConfigureAwait(false);
            }

            return new WorkflowProjectionStep.Advanced(nextState);
        }
        catch (Exception error) when (
            error is WorkflowProjectionLimitExceededException or InvalidDataException or JsonException)
        {
            await RecordWorkflowProjectionFailureAsync(
                key,
                targetSequence,
                error is WorkflowProjectionLimitExceededException ? "limit" : "invalid",
                ct).ConfigureAwait(false);
            throw;
        }
    }

    private Task<WorkflowProjectionInput?> ReadWorkflowProjectionQuantumAsync(
        WorkflowProjectionKey key,
        ulong afterSequence,
        ulong targetSequence,
        WorkflowProjectionCheckpoint? prior,
        bool validateFullReplay,
        CancellationToken ct) =>
        ExecuteReadAsync<WorkflowProjectionInput?>(async (con, token) =>
        {
            await using var transaction = await con.BeginTransactionAsync(token).ConfigureAwait(false);
            var current = ReadWorkflowRun(con, key.ProjectId, key.RunId, transaction);
            if (current is null || current.RunGeneration != key.RunGeneration)
                return null;
            if (targetSequence > current.LatestJournalSequence)
            {
                throw new InvalidDataException(
                    "Workflow projection requested a head beyond the current generation.");
            }

            var budget = new WorkflowProjectionBudget(_workflowProjectionLimits);
            budget.EnsureEventCount(targetSequence);
            if (targetSequence == current.LatestJournalSequence)
                budget.EnsureSerializedInput(current.ProjectionInputBytes);
            await using var command = con.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT " + WorkflowEventDbRow.SelectColumnList + """
                                   FROM workflow_events
                                   WHERE project_id = $1
                                     AND run_id = $2
                                     AND journal_sequence > $3
                                     AND journal_sequence <= $4
                                   ORDER BY journal_sequence
                                   LIMIT $5
                                   """;
            AddParameters(
                command,
                key.ProjectId,
                key.RunId,
                (decimal)afterSequence,
                (decimal)targetSequence,
                checked((long)(targetSequence - afterSequence) + 1));
            var events = new List<WorkflowEventStorageRow>();
            var streamed = await WorkflowEventDbRow.ReadArrowRowsAsync(
                    command,
                    events,
                    static (target, row) => target.Add(ReadWorkflowEvent(row)),
                    token)
                .ConfigureAwait(false);
            WorkflowLifecycleLog.ArrowStreamConsumed(
                _logger,
                "projection_quantum",
                streamed.Batches,
                streamed.Rows);
            var expected = checked((long)(targetSequence - afterSequence));
            if (events.Count != expected)
                throw new InvalidDataException(
                    "Workflow projection journal quantum is incomplete.");

            if (validateFullReplay)
            {
                await ValidateWorkflowProjectionCountersAsync(
                        con,
                        transaction,
                        current,
                        budget,
                        token)
                    .ConfigureAwait(false);
            }

            var projectedRun = WorkflowRunAtSequence(
                current,
                targetSequence,
                events,
                prior);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return new WorkflowProjectionInput(projectedRun, events, budget);
        }, ct);

    private async ValueTask ValidateWorkflowProjectionCountersAsync(
        DuckDBConnection con,
        DbTransaction transaction,
        WorkflowRunStorageRow run,
        WorkflowProjectionBudget budget,
        CancellationToken ct)
    {
        await using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT " + WorkflowEventDbRow.SelectColumnList + """
                               FROM workflow_events
                               WHERE project_id = $1 AND run_id = $2
                               ORDER BY journal_sequence
                               LIMIT $3
                               """;
        AddParameters(
            command,
            run.ProjectId,
            run.RunId,
            budget.Limits.MaxEventsPerRun + 1);
        var validation = new WorkflowProjectionCounterValidation(run, budget);
        var streamed = await WorkflowEventDbRow.ReadArrowRowsAsync(
                command,
                validation,
                static (state, row) => state.Add(ReadWorkflowEvent(row)),
                ct)
            .ConfigureAwait(false);
        WorkflowLifecycleLog.ArrowStreamConsumed(
            _logger,
            "projection_counter_validation",
            streamed.Batches,
            streamed.Rows);
        if (validation.EventCount != run.EventCount ||
            validation.MeasuredInput != run.ProjectionInputBytes)
        {
            throw new InvalidDataException(
                "Workflow projection cumulative counters are invalid.");
        }
    }

    private sealed class WorkflowProjectionCounterValidation(
        WorkflowRunStorageRow run,
        WorkflowProjectionBudget budget)
    {
        public long EventCount { get; private set; }

        public long MeasuredInput { get; private set; } = checked(
            run.ImmutableProjectionInputBytes + run.DynamicProjectionInputBytes);

        public void Add(WorkflowEventStorageRow workflowEvent)
        {
            EventCount++;
            budget.EnsureEventCount(EventCount);
            MeasuredInput = checked(
                MeasuredInput + WorkflowCanonicalization.MeasureEventInput(workflowEvent));
        }
    }

    private static WorkflowRunStorageRow WorkflowRunAtSequence(
        WorkflowRunStorageRow current,
        ulong targetSequence,
        IReadOnlyList<WorkflowEventStorageRow> events,
        WorkflowProjectionCheckpoint? prior)
    {
        if (targetSequence == current.LatestJournalSequence)
            return current;

        var status = prior?.Graph.Run.Status ?? WorkflowRunStatus.Active;
        var endedAt = prior?.Graph.Run.EndedAt;
        var activeAttempt = prior?.Graph.Run.ActiveAttemptId;
        var terminalSeen = status is WorkflowRunStatus.Completed or WorkflowRunStatus.Failed;
        foreach (var workflowEvent in events.OrderBy(static item => item.JournalSequence))
        {
            if (terminalSeen)
                continue;
            if (workflowEvent.Kind is WorkflowJournalEventKind.AttemptStarted)
            {
                activeAttempt = workflowEvent.AttemptId;
                status = WorkflowRunStatus.Active;
                endedAt = null;
            }
            else if (workflowEvent.Kind is WorkflowJournalEventKind.RunCompleted)
            {
                status = EventRunStatus(workflowEvent) ?? WorkflowRunStatus.Completed;
                endedAt = workflowEvent.Timestamp;
                activeAttempt = null;
            }
            else if (workflowEvent.Kind is WorkflowJournalEventKind.TurnInterrupted)
            {
                status = WorkflowRunStatus.Interrupted;
                endedAt = workflowEvent.Timestamp;
            }
            else if (workflowEvent.Kind is WorkflowJournalEventKind.TurnStarted &&
                     status is WorkflowRunStatus.Interrupted)
            {
                status = WorkflowRunStatus.Active;
                endedAt = null;
            }
            terminalSeen = status is WorkflowRunStatus.Completed or WorkflowRunStatus.Failed;
        }
        return current with
        {
            Status = status,
            EndedAt = endedAt,
            LatestJournalSequence = targetSequence,
            ActiveAttemptId = activeAttempt,
            EventCount = checked((long)targetSequence)
        };
    }

    private static WorkflowRunStatus? EventRunStatus(
        WorkflowEventStorageRow workflowEvent)
    {
        if (workflowEvent.DataJson is null)
            return null;
        using var document = JsonDocument.Parse(workflowEvent.DataJson);
        if (document.RootElement.ValueKind is not JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("status", out var status) ||
            status.ValueKind is not JsonValueKind.String)
            return null;
        return status.GetString() switch
        {
            "completed" or "succeeded" => WorkflowRunStatus.Completed,
            "failed" => WorkflowRunStatus.Failed,
            "interrupted" => WorkflowRunStatus.Interrupted,
            _ => null
        };
    }

    private static WorkflowProjectionCheckpoint BuildWorkflowCheckpoint(
        WorkflowProjectionInput input,
        WorkflowProjectionCheckpoint? prior)
    {
        var projectionTime = input.Run.EndedAt ??
                             (input.Events.Count is 0
                                 ? prior?.ProjectionTime
                                 : input.Events[^1].Timestamp) ??
                             input.Run.StartedAt;
        return WorkflowProjectionBuilder.BuildCheckpoint(
            input.Run,
            prior,
            input.Events,
            projectionTime,
            input.Budget);
    }

    private static bool ShouldPersistWorkflowCheckpoint(
        ulong sequence,
        ulong durableSequence,
        ulong forcePersistThroughSequence,
        WorkflowRunStorageRow run)
    {
        if (sequence is 0)
            return false;
        if (durableSequence < forcePersistThroughSequence &&
            sequence >= forcePersistThroughSequence)
            return true;
        if (durableSequence is 0)
            return true;
        if (sequence >= (durableSequence > ulong.MaxValue / 2
                ? ulong.MaxValue
                : durableSequence * 2))
            return true;
        return sequence == run.LatestJournalSequence &&
               run.Status is WorkflowRunStatus.Completed or WorkflowRunStatus.Failed;
    }

    private async Task<bool> PublishWorkflowCheckpointAsync(
        WorkflowRunStorageRow expected,
        WorkflowProjectionCheckpoint checkpoint,
        WorkflowCheckpointBlob blob,
        CancellationToken ct)
    {
        if (!WorkflowCheckpointStore.HasCanonicalCheckpointIdentity(expected))
            throw new InvalidDataException(
                "Workflow checkpoint publication observed an invalid manifest identity.");
        var publishedStorageIdentity =
            WorkflowCheckpointStore.CanonicalStorageIdentity(
                expected.ProjectId,
                expected.RunId,
                expected.RunGeneration,
                checkpoint.JournalSequence,
                blob.CheckpointId);
        await _checkpointManifestMutationGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var publication = await ExecuteMaintenanceWriteAsync(async (con, token) =>
            {
                await using var transaction = await con
                    .BeginTransactionAsync(token)
                    .ConfigureAwait(false);
                var epoch = await AdvanceWorkflowCheckpointEpochAsync(
                        con,
                        transaction,
                        token)
                    .ConfigureAwait(false);
                var changed = false;
                await using (var command = con.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = $"""
                                          UPDATE {WorkflowRunDbRow.TableName}
                                          SET {WorkflowRunDbRow.ActiveCheckpointSequenceColumnName} = $1,
                                              {WorkflowRunDbRow.ActiveCheckpointIdColumnName} = $2,
                                              {WorkflowRunDbRow.ActiveCheckpointStorageKeyColumnName} = $3,
                                              {WorkflowRunDbRow.ActiveCheckpointInputHashColumnName} = $4,
                                              {WorkflowRunDbRow.ActiveCheckpointSemanticFingerprintColumnName} = $5,
                                              {WorkflowRunDbRow.ActiveCheckpointConfigurationFingerprintColumnName} = $6,
                                              {WorkflowRunDbRow.ActiveCheckpointFormatVersionColumnName} = $7,
                                              {WorkflowRunDbRow.ActiveCheckpointByteLengthColumnName} = $8,
                                              {WorkflowRunDbRow.ActiveCheckpointCreatedAtColumnName} = current_timestamp,
                                              {WorkflowRunDbRow.CheckpointManifestEpochColumnName} = $9,
                                              {WorkflowRunDbRow.ProjectionFailureSequenceColumnName} =
                                                  CASE WHEN {WorkflowRunDbRow.ProjectionFailureSequenceColumnName} <= $1
                                                      THEN NULL ELSE {WorkflowRunDbRow.ProjectionFailureSequenceColumnName} END,
                                              {WorkflowRunDbRow.ProjectionFailureKindColumnName} =
                                                  CASE WHEN {WorkflowRunDbRow.ProjectionFailureSequenceColumnName} <= $1
                                                      THEN NULL ELSE {WorkflowRunDbRow.ProjectionFailureKindColumnName} END,
                                              {WorkflowRunDbRow.ProjectionFailureConfigurationColumnName} =
                                                  CASE WHEN {WorkflowRunDbRow.ProjectionFailureSequenceColumnName} <= $1
                                                      THEN NULL ELSE {WorkflowRunDbRow.ProjectionFailureConfigurationColumnName} END,
                                              {WorkflowRunDbRow.ProjectionFailureSemanticColumnName} =
                                                  CASE WHEN {WorkflowRunDbRow.ProjectionFailureSequenceColumnName} <= $1
                                                      THEN NULL ELSE {WorkflowRunDbRow.ProjectionFailureSemanticColumnName} END,
                                              {WorkflowRunDbRow.UpdatedAtColumnName} = current_timestamp
                                          WHERE {WorkflowRunDbRow.ProjectIdColumnName} = $10
                                            AND {WorkflowRunDbRow.RunIdColumnName} = $11
                                            AND {WorkflowRunDbRow.RunGenerationColumnName} = $12
                                            AND {WorkflowRunDbRow.ActiveCheckpointSequenceColumnName} = $13
                                            AND {WorkflowRunDbRow.ActiveCheckpointIdColumnName} IS NOT DISTINCT FROM $14
                                            AND {WorkflowRunDbRow.ActiveCheckpointStorageKeyColumnName}
                                                IS NOT DISTINCT FROM $15
                                            AND {WorkflowRunDbRow.CheckpointManifestEpochColumnName} = $16
                                            AND EXISTS (
                                                SELECT 1
                                                FROM {WorkflowRunSummaryDbRow.TableName} AS summary
                                                WHERE summary.{WorkflowRunSummaryDbRow.ProjectIdColumnName} =
                                                      {WorkflowRunDbRow.TableName}.{WorkflowRunDbRow.ProjectIdColumnName}
                                                  AND summary.{WorkflowRunSummaryDbRow.RunIdColumnName} =
                                                      {WorkflowRunDbRow.TableName}.{WorkflowRunDbRow.RunIdColumnName}
                                                  AND summary.{WorkflowRunSummaryDbRow.LatestJournalSequenceColumnName} >= $1)
                                          RETURNING {WorkflowRunDbRow.ActiveCheckpointSequenceColumnName}
                                          """;
                    WorkflowRunDbRow.AddManifestPublishCasParameters(
                        command,
                        checkpoint.JournalSequence,
                        blob.CheckpointId,
                        publishedStorageIdentity,
                        checkpoint.RunInputHash,
                        checkpoint.ProjectorSemanticFingerprint,
                        checkpoint.ProjectionConfigurationFingerprint,
                        checkpoint.FormatVersion,
                        blob.Length,
                        epoch,
                        expected.ProjectId,
                        expected.RunId,
                        expected.RunGeneration,
                        expected.ActiveCheckpointSequence,
                        expected.ActiveCheckpointId,
                        expected.ActiveCheckpointStorageKey,
                        expected.CheckpointManifestEpoch);
                    await using var reader = await command
                        .ExecuteReaderAsync(token)
                        .ConfigureAwait(false);
                    changed = await reader.ReadAsync(token).ConfigureAwait(false);
                }

                if (!changed)
                {
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                    return (
                        Published: false,
                        RepairCompleted: false,
                        Mutation: (WorkflowCheckpointManifestMutation?)null);
                }

                await using var completeRepair = con.CreateCommand();
                completeRepair.Transaction = transaction;
                completeRepair.CommandText = """
                                             DELETE FROM workflow_checkpoint_repairs
                                             WHERE project_id = $1
                                               AND run_id = $2
                                               AND run_generation = $3
                                               AND latest_journal_sequence <= $4
                                             """;
                AddParameters(
                    completeRepair,
                    expected.ProjectId,
                    expected.RunId,
                    expected.RunGeneration,
                    (decimal)checkpoint.JournalSequence);
                var repairCompleted = await completeRepair
                    .ExecuteNonQueryAsync(token)
                    .ConfigureAwait(false) > 0;
                await transaction.CommitAsync(token).ConfigureAwait(false);

                var deltas = new List<WorkflowCheckpointIdentityDelta>(2);
                if (expected.ActiveCheckpointStorageKey is { } previousIdentity &&
                    previousIdentity != publishedStorageIdentity)
                {
                    deltas.Add(new WorkflowCheckpointIdentityDelta(
                        epoch,
                        0,
                        previousIdentity,
                        Active: false));
                }
                deltas.Add(new WorkflowCheckpointIdentityDelta(
                    epoch,
                    1,
                    publishedStorageIdentity,
                    Active: true));
                return (
                    Published: true,
                    RepairCompleted: repairCompleted,
                    Mutation: new WorkflowCheckpointManifestMutation(epoch, deltas));
            }, ct).ConfigureAwait(false);

            if (publication.Mutation is not null)
            {
                await _workflowCheckpointStore.ApplyManifestMutationAsync(
                        publication.Mutation,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            if (publication.RepairCompleted)
            {
                _activatedCheckpointRepairs.TryRemove(
                    new WorkflowProjectionKey(
                        expected.ProjectId,
                        expected.RunId,
                        expected.RunGeneration),
                    out _);
            }
            return publication.Published;
        }
        finally
        {
            _checkpointManifestMutationGate.Release();
        }
    }

    private static async Task<ulong> AdvanceWorkflowCheckpointEpochAsync(
        DuckDBConnection connection,
        DbTransaction transaction,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              UPDATE workflow_checkpoint_clock
                              SET current_epoch = current_epoch + 1
                              WHERE singleton = 0
                              RETURNING current_epoch
                              """;
        await using var reader = await command
            .ExecuteReaderAsync(ct)
            .ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            throw new InvalidDataException(
                "Workflow checkpoint epoch clock is missing.");
        }
        return DuckDbValueReader.ReadUInt64(reader, 0, 0);
    }

    private Task RecordWorkflowProjectionFailureAsync(
        WorkflowProjectionKey key,
        ulong sequence,
        string kind,
        CancellationToken ct) =>
        ExecuteMaintenanceWriteAsync(async (con, token) =>
        {
            await using var command = con.CreateCommand();
            command.CommandText = """
                                  UPDATE workflow_runs
                                  SET projection_failure_sequence = $1,
                                      projection_failure_kind = $2,
                                      projection_failure_configuration = $3,
                                      projection_failure_semantic = $4,
                                      updated_at = current_timestamp
                                  WHERE project_id = $5
                                    AND run_id = $6
                                    AND run_generation = $7
                                    AND EXISTS (
                                        SELECT 1 FROM workflow_run_summaries AS summary
                                        WHERE summary.project_id = workflow_runs.project_id
                                          AND summary.run_id = workflow_runs.run_id
                                          AND summary.latest_journal_sequence >= $1)
                                  """;
            AddParameters(
                command,
                (decimal)sequence,
                kind,
                _workflowProjectionLimits.ConfigurationFingerprint,
                WorkflowProjectionBuilder.SemanticFingerprint,
                key.ProjectId,
                key.RunId,
                key.RunGeneration);
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            return 0;
        }, ct);

    private Task ClearWorkflowProjectionFailureAsync(
        WorkflowProjectionKey key,
        ulong sequence,
        CancellationToken ct) =>
        ExecuteMaintenanceWriteAsync(async (con, token) =>
        {
            await using var command = con.CreateCommand();
            command.CommandText = """
                                  UPDATE workflow_runs
                                  SET projection_failure_sequence = NULL,
                                      projection_failure_kind = NULL,
                                      projection_failure_configuration = NULL,
                                      projection_failure_semantic = NULL,
                                      updated_at = current_timestamp
                                  WHERE project_id = $1
                                    AND run_id = $2
                                    AND run_generation = $3
                                    AND projection_failure_sequence <= $4
                                  """;
            AddParameters(
                command,
                key.ProjectId,
                key.RunId,
                key.RunGeneration,
                (decimal)sequence);
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            return 0;
        }, ct);

    private async Task<bool> ResetWorkflowProjectionManifestAsync(
        WorkflowRunStorageRow observed,
        CancellationToken ct)
    {
        await _checkpointManifestMutationGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var reset = await ExecuteMaintenanceWriteAsync(async (con, token) =>
            {
                await using var transaction = await con
                    .BeginTransactionAsync(token)
                    .ConfigureAwait(false);
                var epoch = await AdvanceWorkflowCheckpointEpochAsync(
                        con,
                        transaction,
                        token)
                    .ConfigureAwait(false);
                ulong? repairTarget = null;
                await using (var clear = con.CreateCommand())
                {
                    clear.Transaction = transaction;
                    clear.CommandText = $"""
                                        UPDATE {WorkflowRunDbRow.TableName}
                                        SET {WorkflowRunDbRow.ActiveCheckpointSequenceColumnName} = 0,
                                            {WorkflowRunDbRow.ActiveCheckpointIdColumnName} = NULL,
                                            {WorkflowRunDbRow.ActiveCheckpointStorageKeyColumnName} = NULL,
                                            {WorkflowRunDbRow.ActiveCheckpointInputHashColumnName} = NULL,
                                            {WorkflowRunDbRow.ActiveCheckpointSemanticFingerprintColumnName} = NULL,
                                            {WorkflowRunDbRow.ActiveCheckpointConfigurationFingerprintColumnName} = NULL,
                                            {WorkflowRunDbRow.ActiveCheckpointFormatVersionColumnName} = NULL,
                                            {WorkflowRunDbRow.ActiveCheckpointByteLengthColumnName} = NULL,
                                            {WorkflowRunDbRow.ActiveCheckpointCreatedAtColumnName} = NULL,
                                            {WorkflowRunDbRow.CheckpointManifestEpochColumnName} = $1,
                                            {WorkflowRunDbRow.ProjectionFailureSequenceColumnName} = NULL,
                                            {WorkflowRunDbRow.ProjectionFailureKindColumnName} = NULL,
                                            {WorkflowRunDbRow.ProjectionFailureConfigurationColumnName} = NULL,
                                            {WorkflowRunDbRow.ProjectionFailureSemanticColumnName} = NULL,
                                            {WorkflowRunDbRow.UpdatedAtColumnName} = current_timestamp
                                        WHERE {WorkflowRunDbRow.ProjectIdColumnName} = $2
                                          AND {WorkflowRunDbRow.RunIdColumnName} = $3
                                          AND {WorkflowRunDbRow.RunGenerationColumnName} = $4
                                          AND {WorkflowRunDbRow.ActiveCheckpointSequenceColumnName} = $5
                                          AND {WorkflowRunDbRow.ActiveCheckpointIdColumnName}
                                              IS NOT DISTINCT FROM $6
                                          AND {WorkflowRunDbRow.ActiveCheckpointStorageKeyColumnName}
                                              IS NOT DISTINCT FROM $7
                                          AND {WorkflowRunDbRow.CheckpointManifestEpochColumnName} = $8
                                        RETURNING {WorkflowRunDbRow.RunIdColumnName}
                                        """;
                    WorkflowRunDbRow.AddManifestResetCasParameters(
                        clear,
                        epoch,
                        observed.ProjectId,
                        observed.RunId,
                        observed.RunGeneration,
                        observed.ActiveCheckpointSequence,
                        observed.ActiveCheckpointId,
                        observed.ActiveCheckpointStorageKey,
                        observed.CheckpointManifestEpoch);
                    await using var reader = await clear
                        .ExecuteReaderAsync(token)
                        .ConfigureAwait(false);
                    if (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        await reader.DisposeAsync().ConfigureAwait(false);
                        await using var head = con.CreateCommand();
                        head.Transaction = transaction;
                        head.CommandText = $"""
                                           SELECT {WorkflowRunSummaryDbRow.LatestJournalSequenceColumnName}
                                           FROM {WorkflowRunSummaryDbRow.TableName}
                                           WHERE {WorkflowRunSummaryDbRow.ProjectIdColumnName} = $1
                                             AND {WorkflowRunSummaryDbRow.RunIdColumnName} = $2
                                           """;
                        AddParameters(head, observed.ProjectId, observed.RunId);
                        var value = await head.ExecuteScalarAsync(token).ConfigureAwait(false) ??
                                    throw new InvalidDataException(
                                        $"Workflow run '{observed.RunId}' has no journal summary.");
                        repairTarget = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
                    }
                }

                if (!repairTarget.HasValue)
                {
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                    return (
                        Changed: false,
                        Mutation: (WorkflowCheckpointManifestMutation?)null);
                }

                if (repairTarget.Value > 0)
                {
                    await using var persist = con.CreateCommand();
                    persist.Transaction = transaction;
                    persist.CommandText = """
                                          INSERT INTO workflow_checkpoint_repairs (
                                              project_id,
                                              run_id,
                                              run_generation,
                                              latest_journal_sequence)
                                          VALUES ($1, $2, $3, $4)
                                          ON CONFLICT (
                                              project_id,
                                              run_id,
                                              run_generation)
                                          DO UPDATE SET
                                              latest_journal_sequence =
                                                  greatest(
                                                      workflow_checkpoint_repairs.latest_journal_sequence,
                                                      excluded.latest_journal_sequence)
                                          """;
                    AddParameters(
                        persist,
                        observed.ProjectId,
                        observed.RunId,
                        observed.RunGeneration,
                        (decimal)repairTarget.Value);
                    await persist.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);
                var mutation =
                    WorkflowCheckpointStore.HasCanonicalManifest(observed) &&
                    observed.ActiveCheckpointStorageKey is { } identity
                        ? new WorkflowCheckpointManifestMutation(
                            epoch,
                            [
                                new WorkflowCheckpointIdentityDelta(
                                    epoch,
                                    0,
                                    identity,
                                    Active: false)
                            ])
                        : null;
                return (Changed: true, Mutation: mutation);
            }, ct).ConfigureAwait(false);

            if (reset.Mutation is not null)
            {
                await _workflowCheckpointStore.ApplyManifestMutationAsync(
                        reset.Mutation,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            return reset.Changed;
        }
        finally
        {
            _checkpointManifestMutationGate.Release();
        }
    }

    private async Task<IReadOnlyList<WorkflowCheckpointRepair>>
        RepairBrokenWorkflowProjectionManifestsAsync(
            IReadOnlyList<WorkflowRunStorageRow> broken,
            CancellationToken ct)
    {
        if (broken.Count is 0)
        {
            return [];
        }

        await _checkpointManifestMutationGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var result = await ExecuteMaintenanceWriteAsync(
            async (con, token) =>
            {
                // Repair only records that a rebuild is owed. It does not touch
                // the manifest, the epoch, or the identity's active state — the
                // successful CAS publication owns that whole transition, so a
                // failed rebuild leaves the previous checkpoint referenced.
                await using var transaction = await con
                    .BeginTransactionAsync(token)
                    .ConfigureAwait(false);
                await using (var prepare = con.CreateCommand())
                {
                    prepare.Transaction = transaction;
                    prepare.CommandText = """
                                          CREATE TEMP TABLE IF NOT EXISTS
                                              workflow_manifest_repair_candidates (
                                                  project_id VARCHAR NOT NULL,
                                                  run_id VARCHAR NOT NULL,
                                                  run_generation VARCHAR NOT NULL,
                                                  active_checkpoint_sequence UBIGINT NOT NULL,
                                                  active_checkpoint_id VARCHAR,
                                                  active_checkpoint_storage_key VARCHAR,
                                                  checkpoint_manifest_epoch UBIGINT NOT NULL,
                                                  PRIMARY KEY (
                                                      project_id,
                                                      run_id,
                                                      run_generation)
                                              );
                                          DELETE FROM workflow_manifest_repair_candidates;
                                          """;
                    await prepare.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                foreach (var observed in broken)
                {
                    await using var insert = con.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = """
                                         INSERT INTO workflow_manifest_repair_candidates
                                         VALUES ($1, $2, $3, $4, $5, $6, $7)
                                         """;
                    AddParameters(
                        insert,
                        observed.ProjectId,
                        observed.RunId,
                        observed.RunGeneration,
                        (decimal)observed.ActiveCheckpointSequence,
                        DbValue(observed.ActiveCheckpointId),
                        DbValue(observed.ActiveCheckpointStorageKey),
                        (decimal)observed.CheckpointManifestEpoch);
                    await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                // The broken manifest is left in place: the scheduled forced
                // rebuild replaces it through normal compare-and-swap
                // publication. Clearing here would destroy the only manifest
                // when the rebuild itself fails and must record a durable
                // projection failure instead.
                var repairs = new List<WorkflowCheckpointRepair>(broken.Count);
                await using (var survey = con.CreateCommand())
                {
                    survey.Transaction = transaction;
                    survey.CommandText =
                        "SELECT " + WorkflowRunDbRow.SelectColumnList + """
                                         FROM workflow_runs
                                         WHERE EXISTS (
                                             SELECT 1
                                             FROM workflow_manifest_repair_candidates AS observed
                                             WHERE observed.project_id = workflow_runs.project_id
                                               AND observed.run_id = workflow_runs.run_id
                                               AND observed.run_generation =
                                                   workflow_runs.run_generation
                                               AND observed.active_checkpoint_sequence =
                                                   workflow_runs.active_checkpoint_sequence
                                               AND observed.active_checkpoint_id
                                                   IS NOT DISTINCT FROM
                                                   workflow_runs.active_checkpoint_id
                                               AND observed.active_checkpoint_storage_key
                                                   IS NOT DISTINCT FROM
                                                   workflow_runs.active_checkpoint_storage_key
                                               AND observed.checkpoint_manifest_epoch =
                                                   workflow_runs.checkpoint_manifest_epoch)
                                         """;
                    await using var reader = await survey
                        .ExecuteReaderAsync(token)
                        .ConfigureAwait(false);
                    var surveyedRows = new List<WorkflowRunDbRow>();
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                        surveyedRows.Add(WorkflowRunDbRow.MapFromReader(reader));
                    await reader.DisposeAsync().ConfigureAwait(false);
                    foreach (var surveyedEnvelope in surveyedRows)
                    {
                        var surveyed = ReadWorkflowRun(
                            con,
                            surveyedEnvelope,
                            transaction);
                        repairs.Add(new WorkflowCheckpointRepair(
                            new WorkflowProjectionKey(
                                surveyed.ProjectId,
                                surveyed.RunId,
                                surveyed.RunGeneration),
                            surveyed.LatestJournalSequence));
                    }
                }

                if (repairs.Count is 0)
                {
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                    return (IReadOnlyList<WorkflowCheckpointRepair>)repairs;
                }

                foreach (var repair in repairs)
                {
                    await using var persist = con.CreateCommand();
                    persist.Transaction = transaction;
                    persist.CommandText = """
                                          INSERT INTO workflow_checkpoint_repairs (
                                              project_id,
                                              run_id,
                                              run_generation,
                                              latest_journal_sequence)
                                          VALUES ($1, $2, $3, $4)
                                          ON CONFLICT (
                                              project_id,
                                              run_id,
                                              run_generation)
                                          DO UPDATE SET
                                              latest_journal_sequence =
                                                  greatest(
                                                      workflow_checkpoint_repairs.latest_journal_sequence,
                                                      excluded.latest_journal_sequence)
                                          """;
                    AddParameters(
                        persist,
                        repair.Key.ProjectId,
                        repair.Key.RunId,
                        repair.Key.RunGeneration,
                        (decimal)repair.LatestJournalSequence);
                    await persist.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);

                // The old identity stays active until the rebuild publishes its
                // replacement: PublishWorkflowCheckpointAsync emits the
                // old-inactive/new-active pair inside the successful
                // compare-and-swap. Deactivating here would expose the still
                // referenced blob to the sweep whenever the rebuild fails.
                return (IReadOnlyList<WorkflowCheckpointRepair>)repairs;
            },
            ct).ConfigureAwait(false);
            return result;
        }
        finally
        {
            _checkpointManifestMutationGate.Release();
        }
    }

    private async Task DrainWorkflowCheckpointRepairsAsync(CancellationToken ct)
    {
        var repairs = await ExecuteReadAsync<IReadOnlyList<WorkflowCheckpointRepair>>(
            con =>
            {
                using var command = con.CreateCommand();
                command.CommandText =
                    "SELECT " + WorkflowCheckpointRepairDbRow.SelectColumnList + """
                                      FROM workflow_checkpoint_repairs
                                      ORDER BY project_id, run_id, run_generation
                                      LIMIT 256
                                      """;
                using var reader = command.ExecuteReader();
                var rows = new List<WorkflowCheckpointRepair>();
                while (reader.Read())
                {
                    var repairRow = WorkflowCheckpointRepairDbRow.MapFromReader(reader);
                    rows.Add(new WorkflowCheckpointRepair(
                        new WorkflowProjectionKey(
                            repairRow.ProjectId,
                            repairRow.RunId,
                            repairRow.RunGeneration),
                        repairRow.LatestJournalSequence));
                }
                return rows;
            },
            ct).ConfigureAwait(false);

        foreach (var repair in repairs)
        {
            var current = await GetWorkflowRunAsync(
                    repair.Key.ProjectId,
                    repair.Key.RunId,
                    ct)
                .ConfigureAwait(false);
            if (current is null ||
                current.RunGeneration != repair.Key.RunGeneration)
            {
                var removed = await ExecuteMaintenanceWriteAsync(
                    async (con, token) =>
                    {
                        await using var command = con.CreateCommand();
                        command.CommandText = """
                                              DELETE FROM workflow_checkpoint_repairs
                                              WHERE project_id = $1
                                                AND run_id = $2
                                                AND run_generation = $3
                                                AND NOT EXISTS (
                                                    SELECT 1
                                                    FROM workflow_runs AS run
                                                    WHERE run.project_id =
                                                          workflow_checkpoint_repairs.project_id
                                                      AND run.run_id =
                                                          workflow_checkpoint_repairs.run_id
                                                      AND run.run_generation =
                                                          workflow_checkpoint_repairs.run_generation)
                                              """;
                        AddParameters(
                            command,
                            repair.Key.ProjectId,
                            repair.Key.RunId,
                            repair.Key.RunGeneration);
                        return await command
                            .ExecuteNonQueryAsync(token)
                            .ConfigureAwait(false);
                    },
                    ct).ConfigureAwait(false);
                if (removed > 0)
                    _activatedCheckpointRepairs.TryRemove(repair.Key, out _);
                continue;
            }

            var target = await ExecuteMaintenanceWriteAsync<ulong?>(
                async (con, token) =>
                {
                    await using var command = con.CreateCommand();
                    command.CommandText = """
                                          UPDATE workflow_checkpoint_repairs
                                          SET latest_journal_sequence =
                                              greatest(latest_journal_sequence, $1)
                                          WHERE project_id = $2
                                            AND run_id = $3
                                            AND run_generation = $4
                                          RETURNING latest_journal_sequence
                                          """;
                    AddParameters(
                        command,
                        (decimal)current.LatestJournalSequence,
                        repair.Key.ProjectId,
                        repair.Key.RunId,
                        repair.Key.RunGeneration);
                    await using var reader = await command
                        .ExecuteReaderAsync(token)
                        .ConfigureAwait(false);
                    return await reader.ReadAsync(token).ConfigureAwait(false)
                        ? DuckDbValueReader.ReadUInt64(reader, 0, 0)
                        : null;
                },
                ct).ConfigureAwait(false);
            if (!target.HasValue)
                continue;

            if (_activatedCheckpointRepairs.TryAdd(repair.Key, 0))
            {
                try
                {
                    await _workflowProjectionRuntime
                        .RetireAsync(repair.Key)
                        .WaitAsync(ct)
                        .ConfigureAwait(false);
                }
                catch
                {
                    _activatedCheckpointRepairs.TryRemove(repair.Key, out _);
                    throw;
                }
            }
            _workflowProjectionRuntime.TrySchedule(
                repair.Key,
                target.Value,
                target.Value);
        }
    }

    private void ThrowPersistedWorkflowProjectionFailure(
        WorkflowRunStorageRow run,
        ulong requestedSequence)
    {
        if (run.ProjectionFailureSequence != requestedSequence ||
            run.ProjectionFailureConfiguration !=
            _workflowProjectionLimits.ConfigurationFingerprint ||
            run.ProjectionFailureSemantic !=
            WorkflowProjectionBuilder.SemanticFingerprint)
            return;
        if (run.ProjectionFailureKind == "limit")
        {
            throw new WorkflowProjectionLimitExceededException(
                "Workflow projection could not advance within its configured limits.");
        }
        throw new InvalidDataException("Workflow projection recovery failed.");
    }

    // Advances reconciliation by one bounded step and reports whether more work
    // remains. The store never drives this itself: a single owner
    // (WorkflowCheckpointReconciliationService in the collector host) schedules
    // it, so cursor and phase progress can never be split between a background
    // pass and an explicit caller.
    public async Task<bool> ReconcileWorkflowCheckpointsAsync(CancellationToken ct = default)
    {
        await _checkpointReconciliationGate.WaitAsync(ct).ConfigureAwait(false);
        var continuation = false;
        try
        {
            await DrainWorkflowCheckpointRepairsAsync(ct).ConfigureAwait(false);
            if (_checkpointReconciliationPhase is
                WorkflowCheckpointReconciliationPhase.Manifests)
            {
                var projectCursor = _checkpointManifestProjectCursor;
                var runCursor = _checkpointManifestRunCursor;
                if (projectCursor is null)
                {
                    await _checkpointManifestMutationGate
                        .WaitAsync(ct)
                        .ConfigureAwait(false);
                    try
                    {
                        _checkpointReconciliationEpoch =
                            await ReadWorkflowCheckpointEpochAsync(ct)
                                .ConfigureAwait(false);
                        await _workflowCheckpointStore
                            .BeginReconciliationCycleAsync(
                                _checkpointReconciliationEpoch,
                                ct)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        _checkpointManifestMutationGate.Release();
                    }
                }

                var pageLimit = Math.Max(
                    1,
                    _workflowProjectionLimits.CheckpointSweepLimit);
                var manifests = await ExecuteReadAsync<IReadOnlyList<WorkflowRunStorageRow>>(con =>
                {
                    using var command = con.CreateCommand();
                    if (projectCursor is null)
                    {
                        command.CommandText = "SELECT " + WorkflowRunDbRow.SelectColumnList + """
                                               FROM workflow_runs
                                               WHERE active_checkpoint_id IS NOT NULL
                                                 AND checkpoint_manifest_epoch <= $2
                                               ORDER BY project_id, run_id
                                               LIMIT $1
                                               """;
                        AddParameters(
                            command,
                            pageLimit,
                            (decimal)_checkpointReconciliationEpoch);
                    }
                    else
                    {
                        command.CommandText = "SELECT " + WorkflowRunDbRow.SelectColumnList + """
                                               FROM workflow_runs
                                               WHERE active_checkpoint_id IS NOT NULL
                                                 AND checkpoint_manifest_epoch <= $4
                                                 AND (
                                                     project_id > $1 OR
                                                     (project_id = $1 AND run_id > $2)
                                                 )
                                               ORDER BY project_id, run_id
                                               LIMIT $3
                                               """;
                        AddParameters(
                            command,
                            projectCursor,
                            runCursor!,
                            pageLimit,
                            (decimal)_checkpointReconciliationEpoch);
                    }
                    using var reader = command.ExecuteReader();
                    var persisted = new List<WorkflowRunDbRow>(pageLimit);
                    while (reader.Read())
                        persisted.Add(WorkflowRunDbRow.MapFromReader(reader));
                    reader.Close();
                    var rows = new List<WorkflowRunStorageRow>(persisted.Count);
                    foreach (var row in persisted)
                        rows.Add(ReadWorkflowRun(con, row));
                    return rows;
                }, ct).ConfigureAwait(false);

                var validation = await _workflowCheckpointStore
                    .ValidateManifestsAsync(manifests, ct)
                    .ConfigureAwait(false);
                if (_beforeCheckpointReconciliation is not null)
                {
                    await _beforeCheckpointReconciliation(
                            WorkflowCheckpointReconciliationStage.ManifestValidated,
                            ct)
                        .ConfigureAwait(false);
                }

                var repairs = await RepairBrokenWorkflowProjectionManifestsAsync(
                        validation.BrokenManifests,
                        ct)
                    .ConfigureAwait(false);
                if (repairs.Count > 0)
                {
                    WorkflowLifecycleLog.ReconciliationRepairs(
                        _logger,
                        repairs.Count);
                }
                if (repairs.Count > 0 &&
                    _beforeCheckpointReconciliation is not null)
                {
                    await _beforeCheckpointReconciliation(
                            WorkflowCheckpointReconciliationStage.ManifestRepaired,
                            ct)
                        .ConfigureAwait(false);
                }
                await _workflowCheckpointStore
                    .CommitManifestPageAsync(
                        validation.ValidStorageIdentities,
                        ct)
                    .ConfigureAwait(false);

                if (validation.ProcessedManifests > 0)
                {
                    var last = manifests[validation.ProcessedManifests - 1];
                    _checkpointManifestProjectCursor = last.ProjectId;
                    _checkpointManifestRunCursor = last.RunId;
                }
                var hasMoreManifests =
                    validation.ProcessedManifests < manifests.Count ||
                    manifests.Count == pageLimit;
                if (!hasMoreManifests)
                {
                    _checkpointReconciliationPhase =
                        WorkflowCheckpointReconciliationPhase.Sweep;
                }
                continuation = true;
            }
            else
            {
                var page = await _workflowCheckpointStore
                    .PrepareSweepPageAsync(ct)
                    .ConfigureAwait(false);
                if (_beforeCheckpointReconciliation is not null)
                {
                    await _beforeCheckpointReconciliation(
                            WorkflowCheckpointReconciliationStage.SweepPrepared,
                            ct)
                        .ConfigureAwait(false);
                }
                var pageApplied = await _workflowCheckpointStore
                    .ApplySweepPageAsync(
                        page,
                        ct)
                    .ConfigureAwait(false);
                if (!pageApplied)
                {
                    continuation = true;
                }
                else if (page.SweepComplete)
                {
                    await _checkpointManifestMutationGate
                        .WaitAsync(ct)
                        .ConfigureAwait(false);
                    try
                    {
                        await _workflowCheckpointStore
                            .CompleteReconciliationCycleAsync(ct)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        _checkpointManifestMutationGate.Release();
                    }
                    _checkpointManifestProjectCursor = null;
                    _checkpointManifestRunCursor = null;
                    _checkpointReconciliationPhase =
                        WorkflowCheckpointReconciliationPhase.Manifests;
                }
                else
                {
                    continuation = true;
                }
            }
        }
        catch (WorkflowCheckpointReconciliationRestartException)
        {
            _checkpointManifestProjectCursor = null;
            _checkpointManifestRunCursor = null;
            _checkpointReconciliationPhase =
                WorkflowCheckpointReconciliationPhase.Manifests;
            throw;
        }
        finally
        {
            _checkpointReconciliationGate.Release();
        }

        await DrainWorkflowCheckpointRepairsAsync(ct).ConfigureAwait(false);
        return continuation;
    }

    private Task<ulong> ReadWorkflowCheckpointEpochAsync(CancellationToken ct) =>
        ExecuteReadAsync(static con =>
        {
            using var command = con.CreateCommand();
            command.CommandText = """
                                  SELECT current_epoch
                                  FROM workflow_checkpoint_clock
                                  WHERE singleton = 0
                                  """;
            var value = command.ExecuteScalar() ??
                        throw new InvalidDataException(
                            "Workflow checkpoint epoch clock is missing.");
            return Convert.ToUInt64(value, CultureInfo.InvariantCulture);
        }, ct);

    private sealed record WorkflowProjectionInput(
        WorkflowRunStorageRow Run,
        IReadOnlyList<WorkflowEventStorageRow> Events,
        WorkflowProjectionBudget Budget);
}

internal sealed class WorkflowCheckpointWriteStream(
    Stream destination,
    int maximumBytes) : Stream
{
    private readonly IncrementalHash _hash =
        IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private long _bytesWritten;
    private bool _completed;

    public long BytesWritten => _bytesWritten;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => _bytesWritten;

    public override long Position
    {
        get => _bytesWritten;
        set => throw new NotSupportedException();
    }

    public string CompleteDigest()
    {
        if (_completed)
            throw new InvalidOperationException("Workflow checkpoint digest is already complete.");
        _completed = true;
        return Convert.ToHexStringLower(_hash.GetHashAndReset());
    }

    public override void Flush() => destination.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        destination.FlushAsync(cancellationToken);

    public override void Write(byte[] buffer, int offset, int count) =>
        Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureWritable(buffer.Length);
        destination.Write(buffer);
        _hash.AppendData(buffer);
        _bytesWritten += buffer.Length;
    }

    public override void WriteByte(byte value)
    {
        Span<byte> buffer = stackalloc byte[1];
        buffer[0] = value;
        Write(buffer);
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        EnsureWritable(buffer.Length);
        await destination.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _hash.AppendData(buffer.Span);
        _bytesWritten += buffer.Length;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _hash.Dispose();
        base.Dispose(disposing);
    }

    private void EnsureWritable(int count)
    {
        if (_completed)
            throw new InvalidOperationException("Workflow checkpoint digest is already complete.");
        if ((long)count > (long)maximumBytes - _bytesWritten)
        {
            throw new WorkflowProjectionLimitExceededException(
                $"Workflow projection checkpoint exceeds the configured maximum of " +
                $"{maximumBytes} bytes.");
        }
    }
}

internal sealed class WorkflowCheckpointMemoryStream(int maximumBytes) : Stream
{
    private const int SegmentSize = 16 * 1024;
    private readonly List<byte[]> _segments = [];
    private int _allocatedBytes;
    private int _length;
    private int _segmentOffset;

    public int AllocatedBytes => _allocatedBytes;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => _length;

    public override long Position
    {
        get => _length;
        set => throw new NotSupportedException();
    }

    public byte[] ToArray()
    {
        var result = GC.AllocateUninitializedArray<byte>(_length);
        var copied = 0;
        foreach (var segment in _segments)
        {
            var count = Math.Min(segment.Length, _length - copied);
            if (count is 0)
                break;
            segment.AsSpan(0, count).CopyTo(result.AsSpan(copied));
            copied += count;
        }
        return result;
    }

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if ((long)buffer.Length > (long)maximumBytes - _length)
        {
            throw new WorkflowProjectionLimitExceededException(
                $"Workflow projection checkpoint exceeds the configured maximum of " +
                $"{maximumBytes} bytes.");
        }

        while (!buffer.IsEmpty)
        {
            if (_segments.Count is 0 || _segmentOffset == _segments[^1].Length)
            {
                var segmentLength = Math.Min(SegmentSize, maximumBytes - _allocatedBytes);
                if (segmentLength <= 0)
                {
                    throw new WorkflowProjectionLimitExceededException(
                        $"Workflow projection checkpoint exceeds the configured maximum of " +
                        $"{maximumBytes} bytes.");
                }
                _segments.Add(GC.AllocateUninitializedArray<byte>(segmentLength));
                _allocatedBytes += segmentLength;
                _segmentOffset = 0;
            }

            var count = Math.Min(buffer.Length, _segments[^1].Length - _segmentOffset);
            buffer[..count].CopyTo(_segments[^1].AsSpan(_segmentOffset));
            buffer = buffer[count..];
            _segmentOffset += count;
            _length += count;
        }
    }

    public override void WriteByte(byte value)
    {
        Span<byte> buffer = stackalloc byte[1];
        buffer[0] = value;
        Write(buffer);
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();
}
