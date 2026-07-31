using System.Globalization;
using System.Text.Json;
using DuckDB.NET.Data;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Workflow;

namespace Qyl.Collector.Storage;

internal sealed partial class DuckDbStore
{
    public async Task<WorkflowRunStorageRow> CreateWorkflowRunAsync(
        WorkflowRunStorageRow run,
        CancellationToken ct = default)
    {
        run = WorkflowCanonicalization.Normalize(run);
        var immutableInputBytes = WorkflowCanonicalization.MeasureImmutableRunInput(run);
        var dynamicInputBytes = WorkflowCanonicalization.MeasureDynamicRunInput(run);
        var inputBytes = checked(immutableInputBytes + dynamicInputBytes);
        var budget = new WorkflowProjectionBudget(_workflowProjectionLimits);
        budget.EnsureSerializedInput(inputBytes);
        var persistedRun = run with
        {
            RunGeneration = Guid.NewGuid().ToString("N"),
            ProjectionInputBytes = inputBytes,
            ImmutableProjectionInputBytes = immutableInputBytes,
            DynamicProjectionInputBytes = dynamicInputBytes
        };
        return await ExecuteWriteAsync(async (con, token) =>
        {
            await using var transaction = await con.BeginTransactionAsync(token).ConfigureAwait(false);
            var existing = ReadWorkflowRun(
                con,
                persistedRun.ProjectId,
                persistedRun.RunId,
                transaction);
            if (existing is not null)
            {
                if (existing.ThreadId != persistedRun.ThreadId ||
                    existing.Title != persistedRun.Title ||
                    existing.StartedAt != persistedRun.StartedAt ||
                    existing.MetadataJson != persistedRun.MetadataJson)
                {
                    throw new WorkflowRunConflictException(
                        $"Workflow run '{persistedRun.RunId}' already exists with different immutable metadata.");
                }
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return existing;
            }

            await using (var command = con.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = WorkflowRunDbRow.BuildMultiRowInsertSql(1);
                WorkflowRunDbRow.AddParameters(command, new WorkflowRunDbRow
                {
                    ProjectId = persistedRun.ProjectId,
                    RunId = persistedRun.RunId,
                    RunGeneration = persistedRun.RunGeneration,
                    ThreadId = persistedRun.ThreadId,
                    Title = persistedRun.Title,
                    Status = RunStatus(persistedRun.Status),
                    StartedAt = persistedRun.StartedAt,
                    EndedAt = persistedRun.EndedAt,
                    LatestJournalSequence = persistedRun.LatestJournalSequence,
                    EventCount = persistedRun.EventCount,
                    ProjectionInputBytes = persistedRun.ProjectionInputBytes,
                    ImmutableProjectionInputBytes = persistedRun.ImmutableProjectionInputBytes,
                    DynamicProjectionInputBytes = persistedRun.DynamicProjectionInputBytes,
                    ActiveAttemptId = persistedRun.ActiveAttemptId,
                    NextCommandSequence = persistedRun.NextCommandSequence,
                    NextControlEventSourceSequence = persistedRun.NextControlEventSourceSequence,
                    ActiveCheckpointSequence = 0,
                    ActiveCheckpointId = null,
                    ActiveCheckpointStorageKey = null,
                    ProjectionFailureSequence = null,
                    ProjectionFailureKind = null,
                    ProjectionFailureConfiguration = null,
                    ProjectionFailureSemantic = null,
                    MetadataJson = persistedRun.MetadataJson
                });
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            var created = ReadWorkflowRun(
                              con,
                              persistedRun.ProjectId,
                              persistedRun.RunId,
                              transaction)
                ?? throw new InvalidOperationException(
                    "Workflow run insert did not produce a readable row.");
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return created;
        }, ct).ConfigureAwait(false);
    }

    public Task<WorkflowRunStorageRow?> GetWorkflowRunAsync(
        string projectId,
        string runId,
        CancellationToken ct = default) =>
        ExecuteReadAsync(con => ReadWorkflowRun(con, projectId, runId), ct);

    public Task<IReadOnlyList<WorkflowRunStorageRow>> ListWorkflowRunsAsync(
        string projectId,
        WorkflowRunStatus? status,
        int limit,
        int offset,
        CancellationToken ct = default) =>
        ExecuteReadAsync<IReadOnlyList<WorkflowRunStorageRow>>(con =>
        {
            using var command = con.CreateCommand();
            command.CommandText = "SELECT " + WorkflowRunDbRow.SelectColumnList + """
                                   FROM workflow_runs
                                   WHERE project_id = $1
                                     AND ($2 IS NULL OR status = $2)
                                   ORDER BY started_at DESC, run_id
                                   LIMIT $3 OFFSET $4
                                   """;
            AddParameters(command, projectId, DbValue(status is null ? null : RunStatus(status.Value)), limit, offset);
            using var reader = command.ExecuteReader();
            var rows = new List<WorkflowRunStorageRow>();
            while (reader.Read())
                rows.Add(ReadWorkflowRun(reader));
            return rows;
        }, ct);

    public async Task<WorkflowAppendResult> AppendWorkflowEventsAsync(
        string projectId,
        string runId,
        string clientId,
        IReadOnlyList<WorkflowEventWrite> events,
        IReadOnlyList<WorkflowContentWrite> content,
        CancellationToken ct = default)
    {
        events = events.Select(WorkflowCanonicalization.Normalize).ToArray();
        var append = await ExecuteWriteAsync(async (con, token) =>
        {
            await using var transaction = await con.BeginTransactionAsync(token).ConfigureAwait(false);
            var run = ReadWorkflowRun(con, projectId, runId, transaction);
            if (run is null)
                throw new KeyNotFoundException($"Workflow run '{runId}' was not found.");

            var uniqueSources = new Dictionary<ulong, string>();
            var uniqueEventIds = new Dictionary<string, WorkflowEventWrite>(StringComparer.Ordinal);
            var orderedEvents = events
                .OrderBy(static item => item.SourceSequence)
                .ThenBy(static item => item.EventId, StringComparer.Ordinal)
                .ToArray();
            var duplicates = 0;
            foreach (var workflowEvent in orderedEvents)
            {
                if (uniqueSources.TryGetValue(workflowEvent.SourceSequence, out var eventId) &&
                    eventId != workflowEvent.EventId)
                {
                    throw new WorkflowEventConflictException(
                        $"Source sequence {workflowEvent.SourceSequence} occurs more than once with different event ids.");
                }
                uniqueSources[workflowEvent.SourceSequence] = workflowEvent.EventId;
                if (uniqueEventIds.TryGetValue(workflowEvent.EventId, out var prior))
                {
                    if (!MatchesImmutableEvent(prior, workflowEvent))
                    {
                        throw new WorkflowEventConflictException(
                            $"Event id '{workflowEvent.EventId}' occurs with different immutable data.");
                    }
                    duplicates++;
                    continue;
                }
                uniqueEventIds.Add(workflowEvent.EventId, workflowEvent);
            }

            var newEvents = new List<WorkflowEventWrite>(uniqueEventIds.Count);
            foreach (var workflowEvent in uniqueEventIds.Values
                         .OrderBy(static item => item.SourceSequence)
                         .ThenBy(static item => item.EventId, StringComparer.Ordinal))
            {
                var duplicateById = FindEventById(
                    con,
                    transaction,
                    projectId,
                    runId,
                    workflowEvent.EventId);
                if (duplicateById is not null)
                {
                    if (!MatchesImmutableEvent(duplicateById, clientId, workflowEvent))
                    {
                        throw new WorkflowEventConflictException(
                            $"Event id '{workflowEvent.EventId}' was already recorded with different immutable data.");
                    }
                    duplicates++;
                    continue;
                }

                var duplicate = FindEventBySource(
                    con,
                    transaction,
                    projectId,
                    runId,
                    clientId,
                    workflowEvent.SourceSequence);
                if (duplicate is not null)
                {
                    if (!MatchesImmutableEvent(duplicate, clientId, workflowEvent))
                    {
                        throw new WorkflowEventConflictException(
                            $"Source sequence {workflowEvent.SourceSequence} was already recorded with different immutable data.");
                    }
                    duplicates++;
                    continue;
                }

                newEvents.Add(workflowEvent);
            }

            if (newEvents.Count is 0)
            {
                var duplicateAcknowledged = ReadAcknowledgedSourceSequence(
                    con,
                    transaction,
                    projectId,
                    runId,
                    clientId);
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return (
                    Result: new WorkflowAppendResult(0, duplicates, duplicateAcknowledged, null, null),
                    Head: run.LatestJournalSequence,
                    Generation: run.RunGeneration,
                    ScheduleProjection: false);
            }
            if (run.Status is WorkflowRunStatus.Completed or WorkflowRunStatus.Failed)
            {
                throw new WorkflowEventConflictException(
                    $"Workflow run '{runId}' generation '{run.RunGeneration}' is terminal.");
            }

            var eventCount = checked(run.EventCount + newEvents.Count);
            var budget = new WorkflowProjectionBudget(_workflowProjectionLimits);
            budget.EnsureEventCount(eventCount);
            var latest = checked(run.LatestJournalSequence + (ulong)newEvents.Count);
            var activeAttempt = run.ActiveAttemptId;
            var status = run.Status;
            var endedAt = run.EndedAt;
            var terminalSeen = status is WorkflowRunStatus.Completed or WorkflowRunStatus.Failed;
            foreach (var workflowEvent in newEvents)
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

            var updatedRun = run with
            {
                Status = status,
                EndedAt = endedAt,
                LatestJournalSequence = latest,
                ActiveAttemptId = activeAttempt,
                EventCount = eventCount
            };
            var dynamicInputBytes = WorkflowCanonicalization.MeasureDynamicRunInput(updatedRun);
            var projectionInputBytes = checked(
                run.ProjectionInputBytes -
                run.DynamicProjectionInputBytes +
                dynamicInputBytes);
            foreach (var workflowEvent in newEvents)
            {
                projectionInputBytes = checked(
                    projectionInputBytes +
                    WorkflowCanonicalization.MeasureEventInput(
                        projectId,
                        runId,
                        clientId,
                        workflowEvent));
            }
            budget.EnsureSerializedInput(projectionInputBytes);

            foreach (var item in content)
                InsertWorkflowContent(con, transaction, projectId, _workflowContentProtector.Protect(item));

            var capturedInThisBatch = content
                .Select(static item => item.ContentRef)
                .ToHashSet(StringComparer.Ordinal);
            ulong? firstJournalSequence = null;
            ulong? lastJournalSequence = null;
            var journalSequence = run.LatestJournalSequence;

            foreach (var workflowEvent in newEvents)
            {
                EnsureContentReferencesExist(
                    con,
                    transaction,
                    projectId,
                    runId,
                    capturedInThisBatch,
                    workflowEvent.ContentRefs);
                journalSequence++;
                InsertWorkflowEvent(
                    con,
                    transaction,
                    projectId,
                    runId,
                    journalSequence,
                    clientId,
                    workflowEvent);
                firstJournalSequence ??= journalSequence;
                lastJournalSequence = journalSequence;
            }

            await using (var update = con.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = """
                                     UPDATE workflow_runs
                                     SET latest_journal_sequence = $1,
                                         active_attempt_id = $2,
                                         status = $3,
                                         ended_at = $4,
                                         event_count = $5,
                                         projection_input_bytes = $6,
                                         dynamic_projection_input_bytes = $7,
                                         updated_at = current_timestamp,
                                         last_activity_at = current_timestamp
                                     WHERE project_id = $8 AND run_id = $9 AND run_generation = $10
                                     """;
                AddParameters(
                    update,
                    (decimal)latest,
                    DbValue(activeAttempt),
                    RunStatus(status),
                    endedAt.HasValue ? endedAt.Value.UtcDateTime : DBNull.Value,
                    eventCount,
                    projectionInputBytes,
                    dynamicInputBytes,
                    projectId,
                    runId,
                    run.RunGeneration);
                await update.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            var acknowledged = UpdateWorkflowClientAcknowledgement(
                con,
                transaction,
                projectId,
                runId,
                clientId,
                newEvents);
            await transaction.CommitAsync(token).ConfigureAwait(false);

            return (
                Result: new WorkflowAppendResult(
                    newEvents.Count,
                    duplicates,
                    acknowledged,
                    firstJournalSequence,
                    lastJournalSequence),
                Head: latest,
                Generation: run.RunGeneration,
                ScheduleProjection: true);
        }, ct).ConfigureAwait(false);
        if (append.ScheduleProjection)
        {
            _workflowProjectionRuntime.TrySchedule(
                new WorkflowProjectionKey(projectId, runId, append.Generation),
                append.Head);
        }
        return append.Result;
    }

    public Task<WorkflowEventStoragePage?> ReadWorkflowEventsAsync(
        string projectId,
        string runId,
        ulong afterSequence,
        int limit,
        CancellationToken ct = default) =>
        ExecuteReadAsync<WorkflowEventStoragePage?>(con =>
        {
            var run = ReadWorkflowRun(con, projectId, runId);
            if (run is null)
                return null;

            ulong oldest;
            using (var cursor = con.CreateCommand())
            {
                cursor.CommandText = """
                                     SELECT MIN(journal_sequence)
                                     FROM workflow_events
                                     WHERE project_id = $1 AND run_id = $2
                                     """;
                AddParameters(cursor, projectId, runId);
                var value = cursor.ExecuteScalar();
                oldest = value is null or DBNull
                    ? 0
                    : Convert.ToUInt64(value, CultureInfo.InvariantCulture);
            }

            using var command = con.CreateCommand();
            command.CommandText = "SELECT " + WorkflowEventDbRow.SelectColumnList + """
                                   FROM workflow_events
                                   WHERE project_id = $1 AND run_id = $2 AND journal_sequence > $3
                                   ORDER BY journal_sequence
                                   LIMIT $4
                                   """;
            AddParameters(command, projectId, runId, (decimal)afterSequence, limit);
            using var reader = command.ExecuteReader();
            var rows = new List<WorkflowEventStorageRow>();
            while (reader.Read())
                rows.Add(ReadWorkflowEvent(reader));
            var next = rows.Count is 0 ? afterSequence : rows[^1].JournalSequence;
            return new WorkflowEventStoragePage(
                rows,
                next,
                run.LatestJournalSequence,
                oldest > 0 && afterSequence + 1 < oldest);
        }, ct);

    public async Task<WorkflowGraphSnapshot?> GetWorkflowGraphAsync(
        string projectId,
        string runId,
        string? nodeCursor,
        int nodeLimit,
        string? edgeCursor,
        int edgeLimit,
        CancellationToken ct = default)
    {
        var observed = await GetWorkflowRunAsync(projectId, runId, ct).ConfigureAwait(false);
        if (observed is null)
            return null;
        var checkpoint = await AwaitWorkflowProjectionAsync(observed, ct).ConfigureAwait(false);
        if (checkpoint is null)
            return null;
        var immutableRun = WorkflowProjectionBuilder.ToContract(observed);
        var projectionRun = checkpoint.Graph.Run;
        var projectedRun = new WorkflowRun
        {
            RunId = projectionRun.RunId,
            ThreadId = projectionRun.ThreadId,
            Title = projectionRun.Title,
            Status = projectionRun.Status,
            StartedAt = projectionRun.StartedAt,
            EndedAt = projectionRun.EndedAt,
            LatestJournalSequence = projectionRun.LatestJournalSequence,
            ActiveAttemptId = projectionRun.ActiveAttemptId,
            Metadata = immutableRun.Metadata
        };
        var nodes = checkpoint.Graph.Nodes
            .Where(node => string.CompareOrdinal(node.NodeId, nodeCursor ?? string.Empty) > 0)
            .OrderBy(static node => node.NodeId, StringComparer.Ordinal)
            .Take(nodeLimit + 1)
            .ToArray();
        var edges = checkpoint.Graph.Edges
            .Where(edge => string.CompareOrdinal(edge.EdgeId, edgeCursor ?? string.Empty) > 0)
            .OrderBy(static edge => edge.EdgeId, StringComparer.Ordinal)
            .Take(edgeLimit + 1)
            .ToArray();
        var hasMoreNodes = nodes.Length > nodeLimit;
        var hasMoreEdges = edges.Length > edgeLimit;
        return new WorkflowGraphSnapshot
        {
            Run = projectedRun,
            Nodes = nodes.Take(nodeLimit).ToArray(),
            Edges = edges.Take(edgeLimit).ToArray(),
            Statistics = checkpoint.Graph.Statistics,
            JournalSequence = checkpoint.JournalSequence,
            NextNodeCursor = hasMoreNodes ? nodes[nodeLimit - 1].NodeId : null,
            NextEdgeCursor = hasMoreEdges ? edges[edgeLimit - 1].EdgeId : null,
            HasMoreNodes = hasMoreNodes,
            HasMoreEdges = hasMoreEdges,
            TotalNodeCount = checkpoint.Graph.TotalNodeCount,
            TotalEdgeCount = checkpoint.Graph.TotalEdgeCount
        };
    }

    public async Task RebuildWorkflowProjectionAsync(
        string projectId,
        string runId,
        CancellationToken ct = default)
    {
        var run = await GetWorkflowRunAsync(projectId, runId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Workflow run '{runId}' was not found.");
        var key = new WorkflowProjectionKey(projectId, runId, run.RunGeneration);
        if (!await ResetWorkflowProjectionManifestAsync(run, ct).ConfigureAwait(false))
        {
            throw new QylStoreUnavailableException(
                "Workflow projection manifest changed while rebuild was requested.");
        }
        await _workflowProjectionRuntime.RetireAsync(key).ConfigureAwait(false);
        await _workflowProjectionRuntime.WaitForAsync(
            key,
            run.LatestJournalSequence,
            run.LatestJournalSequence,
            ct).ConfigureAwait(false);
    }

    public Task<WorkflowContentReadRow?> GetWorkflowContentAsync(
        string projectId,
        string runId,
        string contentRef,
        CancellationToken ct = default) =>
        ExecuteReadAsync<WorkflowContentReadRow?>(con =>
        {
            using var command = con.CreateCommand();
            command.CommandText = "SELECT " + WorkflowContentDbRow.SelectColumnList + """
                                   FROM workflow_content AS c
                                   WHERE c.project_id = $1 AND c.content_ref = $2
                                     AND EXISTS (
                                         SELECT 1
                                         FROM workflow_content_refs AS r
                                         WHERE r.project_id = c.project_id
                                           AND r.run_id = $3
                                           AND r.content_ref = c.content_ref
                                     )
                                   """;
            AddParameters(command, projectId, contentRef, runId);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return null;
            var content = WorkflowContentDbRow.MapFromReader(reader);
            var protectedContent = new WorkflowContentStorageRow(
                content.ContentRef,
                content.ContentType,
                ParseContentEncoding(content.Encoding),
                content.Nonce,
                content.Tag,
                content.Ciphertext,
                content.UncompressedSize);
            return new WorkflowContentReadRow(
                protectedContent.ContentRef,
                protectedContent.ContentType,
                protectedContent.Encoding,
                _workflowContentProtector.Unprotect(protectedContent),
                protectedContent.SizeBytes);
        }, ct);

    public async Task<WorkflowControlCommandStorageRow?> SubmitWorkflowControlAsync(
        string projectId,
        string runId,
        WorkflowControlAction action,
        string idempotencyKey,
        string? input,
        DateTimeOffset requestedAt,
        CancellationToken ct = default)
    {
        requestedAt = WorkflowCanonicalization.NormalizeTimestamp(requestedAt);
        var submission = await ExecuteWriteAsync(async (con, token) =>
        {
            await using var transaction = await con.BeginTransactionAsync(token).ConfigureAwait(false);
            var run = ReadWorkflowRun(con, projectId, runId, transaction);
            if (run is null)
            {
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return (
                    Command: (WorkflowControlCommandStorageRow?)null,
                    Head: 0UL,
                    Generation: string.Empty,
                    ScheduleProjection: false);
            }
            var existing = ReadControlByIdempotencyKey(
                con,
                transaction,
                projectId,
                runId,
                idempotencyKey);
            if (existing is not null)
            {
                if (existing.Action != action || existing.Input != input)
                    throw new WorkflowControlConflictException(
                        $"Control idempotency key '{idempotencyKey}' was reused with a different command.");
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return (
                    Command: existing,
                    Head: run.LatestJournalSequence,
                    Generation: run.RunGeneration,
                    ScheduleProjection: false);
            }
            var eventCount = checked(run.EventCount + 1);
            var budget = new WorkflowProjectionBudget(_workflowProjectionLimits);
            budget.EnsureEventCount(eventCount);
            var commandId = $"cmd_{Guid.NewGuid():N}";
            var controlEventSequence = run.NextControlEventSourceSequence;
            var controlEvent = CreateControlJournalEvent(
                run,
                commandId,
                action,
                WorkflowControlStatus.Requested,
                requestedAt,
                null,
                controlEventSequence);
            var projectionInputBytes = checked(
                run.ProjectionInputBytes +
                WorkflowCanonicalization.MeasureEventInput(
                    projectId,
                    runId,
                    "collector-control",
                    controlEvent));
            budget.EnsureSerializedInput(projectionInputBytes);
            var commandSequence = run.NextCommandSequence;
            var nextCommandSequence = checked(commandSequence + 1);
            var nextControlEventSourceSequence = checked(controlEventSequence + 1);
            await using (var command = con.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = WorkflowCommandDbRow.BuildMultiRowInsertSql(1);
                WorkflowCommandDbRow.AddParameters(command, new WorkflowCommandDbRow
                {
                    ProjectId = projectId,
                    RunId = runId,
                    CommandId = commandId,
                    CommandSequence = commandSequence,
                    Action = ControlAction(action),
                    Status = ControlStatus(WorkflowControlStatus.Requested),
                    IdempotencyKey = idempotencyKey,
                    Input = input,
                    RequestedAt = requestedAt,
                    UpdatedAt = requestedAt,
                    Error = null
                });
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            AppendControlJournalEvent(
                con,
                transaction,
                run,
                controlEvent,
                eventCount,
                projectionInputBytes,
                nextCommandSequence,
                nextControlEventSourceSequence);
            var created = ReadControl(con, transaction, projectId, runId, commandId);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return (
                Command: created,
                Head: checked(run.LatestJournalSequence + 1),
                Generation: run.RunGeneration,
                ScheduleProjection: true);
        }, ct).ConfigureAwait(false);
        if (submission.ScheduleProjection)
        {
            _workflowProjectionRuntime.TrySchedule(
                new WorkflowProjectionKey(projectId, runId, submission.Generation),
                submission.Head);
        }
        return submission.Command;
    }

    public Task<WorkflowControlCommandStoragePage?> PollWorkflowControlsAsync(
        string projectId,
        string runId,
        ulong afterSequence,
        int limit,
        CancellationToken ct = default) =>
        ExecuteReadAsync<WorkflowControlCommandStoragePage?>(con =>
        {
            if (ReadWorkflowRun(con, projectId, runId) is null)
                return null;
            using var command = con.CreateCommand();
            command.CommandText = "SELECT " + WorkflowCommandDbRow.SelectColumnList + """
                                   FROM workflow_commands
                                   WHERE project_id = $1 AND run_id = $2 AND command_sequence > $3
                                   ORDER BY command_sequence
                                   LIMIT $4
                                   """;
            AddParameters(command, projectId, runId, (decimal)afterSequence, limit);
            using var reader = command.ExecuteReader();
            var commands = new List<WorkflowControlCommandStorageRow>();
            while (reader.Read())
                commands.Add(ReadControl(reader));
            return new WorkflowControlCommandStoragePage(
                commands,
                commands.Count is 0 ? afterSequence : commands[^1].CommandSequence);
        }, ct);

    public async Task<WorkflowControlCommandStorageRow?> UpdateWorkflowControlAsync(
        string projectId,
        string runId,
        string commandId,
        WorkflowControlStatus status,
        string? error,
        DateTimeOffset updatedAt,
        CancellationToken ct = default)
    {
        updatedAt = WorkflowCanonicalization.NormalizeTimestamp(updatedAt);
        var transition = await ExecuteWriteAsync(async (con, token) =>
        {
            await using var transaction = await con.BeginTransactionAsync(token).ConfigureAwait(false);
            var existing = ReadControl(con, transaction, projectId, runId, commandId);
            if (existing is null)
            {
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return (
                    Command: (WorkflowControlCommandStorageRow?)null,
                    Head: 0UL,
                    Generation: string.Empty,
                    ScheduleProjection: false);
            }
            if (existing.Status == status)
            {
                if (existing.Error != error)
                    throw new WorkflowControlConflictException(
                        $"Control command '{commandId}' already has status '{ControlStatus(status)}' with different details.");
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return (
                    Command: existing,
                    Head: 0UL,
                    Generation: string.Empty,
                    ScheduleProjection: false);
            }
            if (!IsControlTransitionAllowed(existing.Status, status))
            {
                throw new WorkflowControlConflictException(
                    $"Control command '{commandId}' cannot transition from '{ControlStatus(existing.Status)}' to '{ControlStatus(status)}'.");
            }

            var run = ReadWorkflowRun(con, projectId, runId, transaction)!;
            var eventCount = checked(run.EventCount + 1);
            var budget = new WorkflowProjectionBudget(_workflowProjectionLimits);
            budget.EnsureEventCount(eventCount);
            var controlEventSequence = run.NextControlEventSourceSequence;
            var controlEvent = CreateControlJournalEvent(
                run,
                commandId,
                existing.Action,
                status,
                updatedAt,
                error,
                controlEventSequence);
            var projectionInputBytes = checked(
                run.ProjectionInputBytes +
                WorkflowCanonicalization.MeasureEventInput(
                    projectId,
                    runId,
                    "collector-control",
                    controlEvent));
            budget.EnsureSerializedInput(projectionInputBytes);
            var nextControlEventSourceSequence = checked(controlEventSequence + 1);
            await using (var command = con.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                                      UPDATE workflow_commands
                                      SET status = $1, error = $2, updated_at = $3
                                      WHERE project_id = $4 AND run_id = $5 AND command_id = $6
                                      """;
                AddParameters(
                    command,
                    ControlStatus(status),
                    DbValue(error),
                    updatedAt.UtcDateTime,
                    projectId,
                    runId,
                    commandId);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            AppendControlJournalEvent(
                con,
                transaction,
                run,
                controlEvent,
                eventCount,
                projectionInputBytes,
                run.NextCommandSequence,
                nextControlEventSourceSequence);
            var updated = ReadControl(con, transaction, projectId, runId, commandId)!;
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return (
                Command: updated,
                Head: checked(run.LatestJournalSequence + 1),
                Generation: run.RunGeneration,
                ScheduleProjection: true);
        }, ct).ConfigureAwait(false);
        if (transition.ScheduleProjection)
        {
            _workflowProjectionRuntime.TrySchedule(
                new WorkflowProjectionKey(projectId, runId, transition.Generation),
                transition.Head);
        }
        return transition.Command;
    }

    private static WorkflowRunStorageRow? ReadWorkflowRun(
        DuckDBConnection con,
        string projectId,
        string runId,
        DbTransaction? transaction = null)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT " + WorkflowRunDbRow.SelectColumnList + """
                               FROM workflow_runs
                               WHERE project_id = $1 AND run_id = $2
                               """;
        AddParameters(command, projectId, runId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadWorkflowRun(reader) : null;
    }

    private static WorkflowRunStorageRow ReadWorkflowRun(DbDataReader reader)
    {
        var row = WorkflowRunDbRow.MapFromReader(reader);
        return new WorkflowRunStorageRow(
            row.ProjectId,
            row.RunId,
            row.ThreadId,
            row.Title,
            ParseRunStatus(row.Status),
            row.StartedAt,
            row.EndedAt,
            row.LatestJournalSequence,
            row.ActiveAttemptId,
            row.MetadataJson,
            row.EventCount,
            row.ProjectionInputBytes,
            row.ImmutableProjectionInputBytes,
            row.DynamicProjectionInputBytes,
            row.NextCommandSequence,
            row.NextControlEventSourceSequence,
            row.ActiveCheckpointSequence,
            row.ActiveCheckpointId,
            row.ActiveCheckpointStorageKey,
            row.CheckpointManifestEpoch,
            row.ProjectionFailureSequence,
            row.ProjectionFailureKind,
            row.ProjectionFailureConfiguration,
            row.RunGeneration,
            row.ProjectionFailureSemantic,
            row.LastActivityAt);
    }

    private static WorkflowEventStorageRow ReadWorkflowEvent(DbDataReader reader)
    {
        var row = WorkflowEventDbRow.MapFromReader(reader);
        return ReadWorkflowEvent(row);
    }

    private static WorkflowEventStorageRow ReadWorkflowEvent(WorkflowEventDbRow row)
    {
        return new WorkflowEventStorageRow(
            row.ProjectId,
            row.RunId,
            row.JournalSequence,
            row.EventId,
            row.ClientId,
            row.SourceSequence,
            row.EventTime,
            ParseEventKind(row.Kind),
            row.ThreadId,
            row.TurnId,
            row.AttemptId,
            row.AgentId,
            row.ParentAgentId,
            row.ReceiverAgentId,
            row.ToolCallId,
            DeserializeStringArray(row.ContentRefsJson),
            row.DataJson);
    }

    private static void InsertWorkflowContent(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        WorkflowContentStorageRow content)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = WorkflowContentDbRow.BuildMultiRowInsertSql(1);
        WorkflowContentDbRow.AddParameters(command, new WorkflowContentDbRow
        {
            ProjectId = projectId,
            ContentRef = content.ContentRef,
            ContentType = content.ContentType,
            Encoding = ContentEncoding(content.Encoding),
            Nonce = content.Nonce,
            Tag = content.Tag,
            Ciphertext = content.Ciphertext,
            UncompressedSize = content.SizeBytes
        });
        command.ExecuteNonQuery();
    }

    private static WorkflowEventStorageRow? FindEventBySource(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        string runId,
        string clientId,
        ulong sourceSequence)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT " + WorkflowEventDbRow.SelectColumnList + """
                               FROM workflow_events
                               WHERE project_id = $1 AND run_id = $2 AND client_id = $3 AND source_sequence = $4
                               """;
        AddParameters(command, projectId, runId, clientId, (decimal)sourceSequence);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadWorkflowEvent(reader) : null;
    }

    private static bool MatchesImmutableEvent(
        WorkflowEventStorageRow recorded,
        string clientId,
        WorkflowEventWrite candidate) =>
        recorded.EventId == candidate.EventId &&
        recorded.ClientId == clientId &&
        recorded.SourceSequence == candidate.SourceSequence &&
        recorded.Timestamp == candidate.Timestamp &&
        recorded.Kind == candidate.Kind &&
        recorded.ThreadId == candidate.ThreadId &&
        recorded.TurnId == candidate.TurnId &&
        recorded.AttemptId == candidate.AttemptId &&
        recorded.AgentId == candidate.AgentId &&
        recorded.ParentAgentId == candidate.ParentAgentId &&
        recorded.ReceiverAgentId == candidate.ReceiverAgentId &&
        recorded.ToolCallId == candidate.ToolCallId &&
        recorded.ContentRefs.SequenceEqual(candidate.ContentRefs, StringComparer.Ordinal) &&
        recorded.DataJson == candidate.DataJson;

    private static bool MatchesImmutableEvent(WorkflowEventWrite left, WorkflowEventWrite right) =>
        left.EventId == right.EventId &&
        left.SourceSequence == right.SourceSequence &&
        left.Timestamp == right.Timestamp &&
        left.Kind == right.Kind &&
        left.ThreadId == right.ThreadId &&
        left.TurnId == right.TurnId &&
        left.AttemptId == right.AttemptId &&
        left.AgentId == right.AgentId &&
        left.ParentAgentId == right.ParentAgentId &&
        left.ReceiverAgentId == right.ReceiverAgentId &&
        left.ToolCallId == right.ToolCallId &&
        left.ContentRefs.SequenceEqual(right.ContentRefs, StringComparer.Ordinal) &&
        left.DataJson == right.DataJson;

    private static WorkflowEventStorageRow? FindEventById(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        string runId,
        string eventId)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT " + WorkflowEventDbRow.SelectColumnList + """
                               FROM workflow_events
                               WHERE project_id = $1 AND run_id = $2 AND event_id = $3
                               """;
        AddParameters(command, projectId, runId, eventId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadWorkflowEvent(reader) : null;
    }

    private static void EnsureContentReferencesExist(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        string runId,
        IReadOnlySet<string> capturedInThisBatch,
        IReadOnlyList<string> contentRefs)
    {
        foreach (var contentRef in contentRefs)
        {
            if (capturedInThisBatch.Contains(contentRef))
                continue;

            using var command = con.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                                  SELECT count(*)
                                  FROM workflow_content_refs
                                  WHERE project_id = $1 AND run_id = $2 AND content_ref = $3
                                  """;
            AddParameters(command, projectId, runId, contentRef);
            if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) is 0)
                throw new WorkflowEventConflictException(
                    $"Workflow event references content '{contentRef}' that this run has not captured.");
        }
    }

    private static void InsertWorkflowEvent(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        string runId,
        ulong journalSequence,
        string clientId,
        WorkflowEventWrite workflowEvent)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = WorkflowEventDbRow.BuildMultiRowInsertSql(1);
        WorkflowEventDbRow.AddParameters(command, new WorkflowEventDbRow
        {
            ProjectId = projectId,
            RunId = runId,
            JournalSequence = journalSequence,
            EventId = workflowEvent.EventId,
            ClientId = clientId,
            SourceSequence = workflowEvent.SourceSequence,
            EventTime = workflowEvent.Timestamp,
            Kind = EventKind(workflowEvent.Kind),
            ThreadId = workflowEvent.ThreadId,
            TurnId = workflowEvent.TurnId,
            AttemptId = workflowEvent.AttemptId,
            AgentId = workflowEvent.AgentId,
            ParentAgentId = workflowEvent.ParentAgentId,
            ReceiverAgentId = workflowEvent.ReceiverAgentId,
            ToolCallId = workflowEvent.ToolCallId,
            ContentRefsJson = SerializeStringArray(workflowEvent.ContentRefs),
            DataJson = workflowEvent.DataJson
        });
        command.ExecuteNonQuery();

        foreach (var contentRef in workflowEvent.ContentRefs)
        {
            using var contentRefCommand = con.CreateCommand();
            contentRefCommand.Transaction = transaction;
            contentRefCommand.CommandText = WorkflowContentReferenceDbRow.BuildMultiRowInsertSql(1);
            WorkflowContentReferenceDbRow.AddParameters(
                contentRefCommand,
                new WorkflowContentReferenceDbRow
                {
                    ProjectId = projectId,
                    RunId = runId,
                    EventId = workflowEvent.EventId,
                    ContentRef = contentRef
                });
            contentRefCommand.ExecuteNonQuery();
        }
    }

    private static ulong ReadAcknowledgedSourceSequence(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        string runId,
        string clientId) =>
        ReadWorkflowClientJournal(con, transaction, projectId, runId, clientId)
            ?.AcknowledgedSourceSequence ?? 0;

    private static WorkflowClientJournalDbRow? ReadWorkflowClientJournal(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        string runId,
        string clientId)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT " + WorkflowClientJournalDbRow.SelectColumnList + """
                              FROM workflow_client_journal
                              WHERE project_id = $1 AND run_id = $2 AND client_id = $3
                              """;
        AddParameters(command, projectId, runId, clientId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? WorkflowClientJournalDbRow.MapFromReader(reader) : null;
    }

    private static ulong UpdateWorkflowClientAcknowledgement(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        string runId,
        string clientId,
        IReadOnlyList<WorkflowEventWrite> newEvents)
    {
        var clientState = ReadWorkflowClientJournal(
            con,
            transaction,
            projectId,
            runId,
            clientId);
        var acknowledged = clientState?.AcknowledgedSourceSequence ?? 0;
        foreach (var workflowEvent in newEvents.OrderBy(static item => item.SourceSequence))
        {
            var sequence = workflowEvent.SourceSequence;
            if (sequence <= acknowledged)
                continue;
            if (acknowledged < ulong.MaxValue && sequence == acknowledged + 1)
            {
                acknowledged = sequence;
                if (acknowledged < ulong.MaxValue &&
                    ReadWorkflowClientRangeByStart(
                        con,
                        transaction,
                        projectId,
                        runId,
                        clientId,
                        acknowledged + 1) is { } contiguous)
                {
                    acknowledged = contiguous.RangeEnd;
                    DeleteWorkflowClientRange(
                        con,
                        transaction,
                        projectId,
                        runId,
                        clientId,
                        contiguous.RangeStart);
                }
                continue;
            }

            var left = sequence > 0
                ? ReadWorkflowClientRangeByEnd(
                    con,
                    transaction,
                    projectId,
                    runId,
                    clientId,
                    sequence - 1)
                : null;
            var right = sequence < ulong.MaxValue
                ? ReadWorkflowClientRangeByStart(
                    con,
                    transaction,
                    projectId,
                    runId,
                    clientId,
                    sequence + 1)
                : null;
            if (left is not null)
            {
                UpdateWorkflowClientRangeEnd(
                    con,
                    transaction,
                    projectId,
                    runId,
                    clientId,
                    left.RangeStart,
                    right?.RangeEnd ?? sequence);
                if (right is not null)
                {
                    DeleteWorkflowClientRange(
                        con,
                        transaction,
                        projectId,
                        runId,
                        clientId,
                        right.RangeStart);
                }
            }
            else if (right is not null)
            {
                DeleteWorkflowClientRange(
                    con,
                    transaction,
                    projectId,
                    runId,
                    clientId,
                    right.RangeStart);
                InsertWorkflowClientRange(
                    con,
                    transaction,
                    projectId,
                    runId,
                    clientId,
                    sequence,
                    right.RangeEnd);
            }
            else
            {
                InsertWorkflowClientRange(
                    con,
                    transaction,
                    projectId,
                    runId,
                    clientId,
                    sequence,
                    sequence);
            }
        }

        if (clientState is null)
        {
            using var insert = con.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = WorkflowClientJournalDbRow.BuildMultiRowInsertSql(1);
            WorkflowClientJournalDbRow.AddParameters(insert, new WorkflowClientJournalDbRow
            {
                ProjectId = projectId,
                RunId = runId,
                ClientId = clientId,
                AcknowledgedSourceSequence = acknowledged
            });
            insert.ExecuteNonQuery();
        }
        else
        {
            using var update = con.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                                 UPDATE workflow_client_journal
                                 SET acknowledged_source_sequence = $1
                                 WHERE project_id = $2 AND run_id = $3 AND client_id = $4
                                 """;
            AddParameters(update, (decimal)acknowledged, projectId, runId, clientId);
            update.ExecuteNonQuery();
        }
        return acknowledged;
    }

    private static WorkflowClientJournalRangeDbRow? ReadWorkflowClientRangeByStart(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        string runId,
        string clientId,
        ulong rangeStart)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT " + WorkflowClientJournalRangeDbRow.SelectColumnList + """
                               FROM workflow_client_journal_ranges
                               WHERE project_id = $1
                                 AND run_id = $2
                                 AND client_id = $3
                                 AND range_start = $4
                               """;
        AddParameters(command, projectId, runId, clientId, (decimal)rangeStart);
        using var reader = command.ExecuteReader();
        return reader.Read() ? WorkflowClientJournalRangeDbRow.MapFromReader(reader) : null;
    }

    private static WorkflowClientJournalRangeDbRow? ReadWorkflowClientRangeByEnd(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        string runId,
        string clientId,
        ulong rangeEnd)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT " + WorkflowClientJournalRangeDbRow.SelectColumnList + """
                               FROM workflow_client_journal_ranges
                               WHERE project_id = $1
                                 AND run_id = $2
                                 AND client_id = $3
                                 AND range_end = $4
                               """;
        AddParameters(command, projectId, runId, clientId, (decimal)rangeEnd);
        using var reader = command.ExecuteReader();
        return reader.Read() ? WorkflowClientJournalRangeDbRow.MapFromReader(reader) : null;
    }

    private static void InsertWorkflowClientRange(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        string runId,
        string clientId,
        ulong rangeStart,
        ulong rangeEnd)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = WorkflowClientJournalRangeDbRow.BuildMultiRowInsertSql(1);
        WorkflowClientJournalRangeDbRow.AddParameters(command, new WorkflowClientJournalRangeDbRow
        {
            ProjectId = projectId,
            RunId = runId,
            ClientId = clientId,
            RangeStart = rangeStart,
            RangeEnd = rangeEnd
        });
        command.ExecuteNonQuery();
    }

    private static void UpdateWorkflowClientRangeEnd(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        string runId,
        string clientId,
        ulong rangeStart,
        ulong rangeEnd)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              UPDATE workflow_client_journal_ranges
                              SET range_end = $1
                              WHERE project_id = $2
                                AND run_id = $3
                                AND client_id = $4
                                AND range_start = $5
                              """;
        AddParameters(
            command,
            (decimal)rangeEnd,
            projectId,
            runId,
            clientId,
            (decimal)rangeStart);
        command.ExecuteNonQuery();
    }

    private static void DeleteWorkflowClientRange(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        string runId,
        string clientId,
        ulong rangeStart)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              DELETE FROM workflow_client_journal_ranges
                              WHERE project_id = $1
                                AND run_id = $2
                                AND client_id = $3
                                AND range_start = $4
                              """;
        AddParameters(command, projectId, runId, clientId, (decimal)rangeStart);
        command.ExecuteNonQuery();
    }

    private static WorkflowControlCommandStorageRow? ReadControlByIdempotencyKey(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        string runId,
        string idempotencyKey)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT " + WorkflowCommandDbRow.SelectColumnList + """
                               FROM workflow_commands
                               WHERE project_id = $1 AND run_id = $2 AND idempotency_key = $3
                               """;
        AddParameters(command, projectId, runId, idempotencyKey);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadControl(reader) : null;
    }

    private static WorkflowControlCommandStorageRow? ReadControl(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        string runId,
        string commandId)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT " + WorkflowCommandDbRow.SelectColumnList + """
                               FROM workflow_commands
                               WHERE project_id = $1 AND run_id = $2 AND command_id = $3
                               """;
        AddParameters(command, projectId, runId, commandId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadControl(reader) : null;
    }

    private static WorkflowControlCommandStorageRow ReadControl(DbDataReader reader)
    {
        var row = WorkflowCommandDbRow.MapFromReader(reader);
        return new WorkflowControlCommandStorageRow(
            row.ProjectId,
            row.RunId,
            row.CommandId,
            row.CommandSequence,
            ParseControlAction(row.Action),
            ParseControlStatus(row.Status),
            row.IdempotencyKey,
            row.Input,
            row.RequestedAt,
            row.UpdatedAt,
            row.Error);
    }

    private static WorkflowEventWrite CreateControlJournalEvent(
        WorkflowRunStorageRow run,
        string commandId,
        WorkflowControlAction action,
        WorkflowControlStatus status,
        DateTimeOffset timestamp,
        string? error,
        ulong sourceSequence)
    {
        var statusText = ControlStatus(status);
        var errorJson = error is null
            ? "null"
            : JsonSerializer.Serialize(error, QylSerializerContext.Default.String);
        var dataJson =
            $"{{\"command_id\":{JsonSerializer.Serialize(commandId, QylSerializerContext.Default.String)},\"action\":{JsonSerializer.Serialize(ControlAction(action), QylSerializerContext.Default.String)},\"status\":{JsonSerializer.Serialize(statusText, QylSerializerContext.Default.String)},\"error\":{errorJson}}}";
        return WorkflowCanonicalization.Normalize(new WorkflowEventWrite(
            $"control:{commandId}:{statusText}",
            sourceSequence,
            timestamp,
            ControlEventKind(status),
            run.ThreadId,
            null,
            run.ActiveAttemptId,
            null,
            null,
            null,
            null,
            [],
            dataJson));
    }

    private static void AppendControlJournalEvent(
        DuckDBConnection con,
        DbTransaction transaction,
        WorkflowRunStorageRow run,
        WorkflowEventWrite workflowEvent,
        long eventCount,
        long projectionInputBytes,
        ulong nextCommandSequence,
        ulong nextControlEventSourceSequence)
    {
        var latest = checked(run.LatestJournalSequence + 1);
        InsertWorkflowEvent(
            con,
            transaction,
            run.ProjectId,
            run.RunId,
            latest,
            "collector-control",
            workflowEvent);
        using var update = con.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
                             UPDATE workflow_runs
                             SET latest_journal_sequence = $1,
                                 event_count = $2,
                                 projection_input_bytes = $3,
                                 next_command_sequence = $4,
                                 next_control_event_source_sequence = $5,
                                 updated_at = current_timestamp,
                                 last_activity_at = current_timestamp
                             WHERE project_id = $6 AND run_id = $7 AND run_generation = $8
                             """;
        AddParameters(
            update,
            (decimal)latest,
            eventCount,
            projectionInputBytes,
            (decimal)nextCommandSequence,
            (decimal)nextControlEventSourceSequence,
            run.ProjectId,
            run.RunId,
            run.RunGeneration);
        update.ExecuteNonQuery();
    }

    private static bool IsControlTransitionAllowed(
        WorkflowControlStatus from,
        WorkflowControlStatus to) =>
        from switch
        {
            WorkflowControlStatus.Requested => to is
                WorkflowControlStatus.Accepted or
                WorkflowControlStatus.Rejected or
                WorkflowControlStatus.Failed,
            WorkflowControlStatus.Accepted => to is
                WorkflowControlStatus.Applied or
                WorkflowControlStatus.Failed,
            _ => false
        };

    private static WorkflowJournalEventKind ControlEventKind(WorkflowControlStatus status) =>
        status switch
        {
            WorkflowControlStatus.Requested => WorkflowJournalEventKind.ControlRequested,
            WorkflowControlStatus.Accepted => WorkflowJournalEventKind.ControlAccepted,
            WorkflowControlStatus.Applied => WorkflowJournalEventKind.ControlApplied,
            WorkflowControlStatus.Rejected => WorkflowJournalEventKind.ControlRejected,
            WorkflowControlStatus.Failed => WorkflowJournalEventKind.ControlFailed,
            _ => WorkflowJournalEventKind.ControlFailed
        };

    private static WorkflowRunStatus? EventRunStatus(WorkflowEventWrite workflowEvent)
    {
        if (workflowEvent.DataJson is null)
            return null;
        using var document = JsonDocument.Parse(workflowEvent.DataJson);
        if (document.RootElement.ValueKind is not JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("status", out var status) ||
            status.ValueKind is not JsonValueKind.String)
        {
            return null;
        }
        return status.GetString() switch
        {
            "completed" or "succeeded" => WorkflowRunStatus.Completed,
            "failed" => WorkflowRunStatus.Failed,
            "interrupted" => WorkflowRunStatus.Interrupted,
            _ => null
        };
    }

    private static object DbValue(string? value) => value is null ? DBNull.Value : value;

    private static void AddParameters(DuckDBCommand command, params object[] values)
    {
        foreach (var value in values)
            command.Parameters.Add(new DuckDBParameter { Value = value });
    }

    private static string SerializeStringArray(IReadOnlyList<string> values) =>
        JsonSerializer.Serialize(values.ToArray(), QylSerializerContext.Default.StringArray);

    private static IReadOnlyList<string> DeserializeStringArray(string json) =>
        JsonSerializer.Deserialize(json, QylSerializerContext.Default.StringArray) ?? [];

    private static string RunStatus(WorkflowRunStatus status) => status switch
    {
        WorkflowRunStatus.Active => "active",
        WorkflowRunStatus.Completed => "completed",
        WorkflowRunStatus.Failed => "failed",
        WorkflowRunStatus.Interrupted => "interrupted",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private static WorkflowRunStatus ParseRunStatus(string status) => status switch
    {
        "active" => WorkflowRunStatus.Active,
        "completed" => WorkflowRunStatus.Completed,
        "failed" => WorkflowRunStatus.Failed,
        "interrupted" => WorkflowRunStatus.Interrupted,
        _ => throw new InvalidDataException($"Unknown workflow run status '{status}'.")
    };

    private static string EventKind(WorkflowJournalEventKind kind) =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(kind.ToString());

    private static WorkflowJournalEventKind ParseEventKind(string kind)
    {
        foreach (var value in Enum.GetValues<WorkflowJournalEventKind>())
        {
            if (EventKind(value) == kind)
                return value;
        }
        throw new InvalidDataException($"Unknown workflow event kind '{kind}'.");
    }

    private static string ContentEncoding(WorkflowContentEncoding encoding) => encoding switch
    {
        WorkflowContentEncoding.Utf8 => "utf8",
        WorkflowContentEncoding.Base64 => "base64",
        _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null)
    };

    private static WorkflowContentEncoding ParseContentEncoding(string encoding) => encoding switch
    {
        "utf8" => WorkflowContentEncoding.Utf8,
        "base64" => WorkflowContentEncoding.Base64,
        _ => throw new InvalidDataException($"Unknown workflow content encoding '{encoding}'.")
    };

    private static string ControlAction(WorkflowControlAction action) => action switch
    {
        WorkflowControlAction.Steer => "steer",
        WorkflowControlAction.Interrupt => "interrupt",
        WorkflowControlAction.Resume => "resume",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    private static WorkflowControlAction ParseControlAction(string action) => action switch
    {
        "steer" => WorkflowControlAction.Steer,
        "interrupt" => WorkflowControlAction.Interrupt,
        "resume" => WorkflowControlAction.Resume,
        _ => throw new InvalidDataException($"Unknown workflow control action '{action}'.")
    };

    private static string ControlStatus(WorkflowControlStatus status) => status switch
    {
        WorkflowControlStatus.Requested => "requested",
        WorkflowControlStatus.Accepted => "accepted",
        WorkflowControlStatus.Applied => "applied",
        WorkflowControlStatus.Rejected => "rejected",
        WorkflowControlStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private static WorkflowControlStatus ParseControlStatus(string status) => status switch
    {
        "requested" => WorkflowControlStatus.Requested,
        "accepted" => WorkflowControlStatus.Accepted,
        "applied" => WorkflowControlStatus.Applied,
        "rejected" => WorkflowControlStatus.Rejected,
        "failed" => WorkflowControlStatus.Failed,
        _ => throw new InvalidDataException($"Unknown workflow control status '{status}'.")
    };

}
