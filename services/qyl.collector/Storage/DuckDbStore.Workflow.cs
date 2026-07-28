using System.Globalization;
using System.Text.Json;
using DuckDB.NET.Data;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Workflow;

namespace Qyl.Collector.Storage;

internal sealed partial class DuckDbStore
{
    public Task<WorkflowRunStorageRow> CreateWorkflowRunAsync(
        WorkflowRunStorageRow run,
        CancellationToken ct = default) =>
        ExecuteWriteAsync(async (con, token) =>
        {
            await using var transaction = await con.BeginTransactionAsync(token).ConfigureAwait(false);
            var existing = ReadWorkflowRun(con, run.ProjectId, run.RunId, transaction);
            if (existing is not null)
            {
                if (existing.ThreadId != run.ThreadId ||
                    existing.Title != run.Title ||
                    existing.StartedAt != run.StartedAt ||
                    existing.MetadataJson != run.MetadataJson)
                {
                    throw new WorkflowRunConflictException(
                        $"Workflow run '{run.RunId}' already exists with different immutable metadata.");
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
                    ProjectId = run.ProjectId,
                    RunId = run.RunId,
                    ThreadId = run.ThreadId,
                    Title = run.Title,
                    Status = RunStatus(run.Status),
                    StartedAt = run.StartedAt,
                    EndedAt = run.EndedAt,
                    LatestJournalSequence = run.LatestJournalSequence,
                    ActiveAttemptId = run.ActiveAttemptId,
                    MetadataJson = run.MetadataJson
                });
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            var created = ReadWorkflowRun(con, run.ProjectId, run.RunId, transaction)
                ?? throw new InvalidOperationException("Workflow run insert did not produce a readable row.");
            PersistWorkflowProjection(con, transaction, created, []);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return created;
        }, ct);

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

    public Task<WorkflowAppendResult> AppendWorkflowEventsAsync(
        string projectId,
        string runId,
        string clientId,
        IReadOnlyList<WorkflowEventWrite> events,
        IReadOnlyList<WorkflowContentWrite> content,
        CancellationToken ct = default) =>
        ExecuteWriteAsync(async (con, token) =>
        {
            await using var transaction = await con.BeginTransactionAsync(token).ConfigureAwait(false);
            var run = ReadWorkflowRun(con, projectId, runId, transaction);
            if (run is null)
                throw new KeyNotFoundException($"Workflow run '{runId}' was not found.");

            var uniqueSources = new Dictionary<ulong, string>();
            foreach (var workflowEvent in events)
            {
                if (uniqueSources.TryGetValue(workflowEvent.SourceSequence, out var eventId) &&
                    eventId != workflowEvent.EventId)
                {
                    throw new WorkflowEventConflictException(
                        $"Source sequence {workflowEvent.SourceSequence} occurs more than once with different event ids.");
                }
                uniqueSources[workflowEvent.SourceSequence] = workflowEvent.EventId;
            }

            foreach (var item in content)
                InsertWorkflowContent(con, transaction, projectId, _workflowContentProtector.Protect(item));

            var capturedInThisBatch = content
                .Select(static item => item.ContentRef)
                .ToHashSet(StringComparer.Ordinal);

            var accepted = 0;
            var duplicates = 0;
            ulong? firstJournalSequence = null;
            ulong? lastJournalSequence = null;
            var latest = run.LatestJournalSequence;
            var activeAttempt = run.ActiveAttemptId;
            var status = run.Status;
            var endedAt = run.EndedAt;

            foreach (var workflowEvent in events
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
                    if (duplicateById.ClientId != clientId ||
                        duplicateById.SourceSequence != workflowEvent.SourceSequence)
                    {
                        throw new WorkflowEventConflictException(
                            $"Event id '{workflowEvent.EventId}' was already recorded at a different source position.");
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
                    if (duplicate.EventId != workflowEvent.EventId)
                    {
                        throw new WorkflowEventConflictException(
                            $"Source sequence {workflowEvent.SourceSequence} was already recorded as event '{duplicate.EventId}'.");
                    }
                    duplicates++;
                    continue;
                }

                EnsureContentReferencesExist(
                    con,
                    transaction,
                    projectId,
                    runId,
                    capturedInThisBatch,
                    workflowEvent.ContentRefs);
                latest++;
                InsertWorkflowEvent(
                    con,
                    transaction,
                    projectId,
                    runId,
                    latest,
                    clientId,
                    workflowEvent);
                accepted++;
                firstJournalSequence ??= latest;
                lastJournalSequence = latest;

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
                else if (workflowEvent.Kind is WorkflowJournalEventKind.TurnStarted
                         && status is WorkflowRunStatus.Interrupted)
                {
                    // A resume continues the same attempt, so the next turn starting is the
                    // journal's proof the run is active again — without this, EndedAt stayed
                    // latched from the interrupt until a new attempt, and a resumed run kept
                    // reading as ended. Guarded on Interrupted so a late TurnStarted can never
                    // resurrect a completed run.
                    status = WorkflowRunStatus.Active;
                    endedAt = null;
                }
            }

            if (accepted > 0)
            {
                await using var update = con.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = """
                                     UPDATE workflow_runs
                                     SET latest_journal_sequence = $1,
                                         active_attempt_id = $2,
                                         status = $3,
                                         ended_at = $4,
                                         updated_at = current_timestamp
                                     WHERE project_id = $5 AND run_id = $6
                                     """;
                AddParameters(
                    update,
                    (decimal)latest,
                    DbValue(activeAttempt),
                    RunStatus(status),
                    endedAt.HasValue ? endedAt.Value.UtcDateTime : DBNull.Value,
                    projectId,
                    runId);
                await update.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            var acknowledged = ReadAcknowledgedSourceSequence(
                con,
                transaction,
                projectId,
                runId,
                clientId);
            var updatedRun = ReadWorkflowRun(con, projectId, runId, transaction)!;
            var allEvents = ReadWorkflowEvents(con, projectId, runId, transaction);
            PersistWorkflowProjection(con, transaction, updatedRun, allEvents);
            await transaction.CommitAsync(token).ConfigureAwait(false);

            return new WorkflowAppendResult(
                accepted,
                duplicates,
                acknowledged,
                firstJournalSequence,
                lastJournalSequence);
        }, ct);

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

    public Task<WorkflowGraphSnapshot?> GetWorkflowGraphAsync(
        string projectId,
        string runId,
        string? nodeCursor,
        int nodeLimit,
        string? edgeCursor,
        int edgeLimit,
        CancellationToken ct = default) =>
        ExecuteReadAsync<WorkflowGraphSnapshot?>(con =>
        {
            // Statistics, nodes and edges are three reads of the same projection. Issued
            // outside a transaction they could each land on a different journal position, so a
            // snapshot could carry statistics for one graph, nodes for a second and edges for a
            // third while journal_sequence claimed to describe all of them.
            using var transaction = con.BeginTransaction();
            WorkflowProjectionState? state;
            using (var command = con.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT " + WorkflowProjectionStateDbRow.SelectColumnList + """
                                       FROM workflow_projection_state
                                       WHERE project_id = $1 AND run_id = $2
                                       """;
                AddParameters(command, projectId, runId);
                using var reader = command.ExecuteReader();
                state = reader.Read()
                    ? JsonSerializer.Deserialize(
                        WorkflowProjectionStateDbRow.MapFromReader(reader).GraphJson,
                        WorkflowStorageJsonContext.Default.WorkflowProjectionState)
                    : null;
            }
            if (state is null)
                return null;

            var nodes = new List<WorkflowGraphNode>(nodeLimit + 1);
            using (var command = con.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT " + WorkflowProjectionNodeDbRow.SelectColumnList + """
                                       FROM workflow_projection_nodes
                                       WHERE project_id = $1 AND run_id = $2 AND node_id > $4
                                       ORDER BY node_id
                                       LIMIT $3
                                       """;
                AddParameters(command, projectId, runId, nodeLimit + 1, nodeCursor ?? string.Empty);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var row = WorkflowProjectionNodeDbRow.MapFromReader(reader);
                    nodes.Add(JsonSerializer.Deserialize(
                                  row.NodeJson,
                                  QylSerializerContext.Default.WorkflowGraphNode)
                              ?? throw new InvalidDataException(
                                  $"Workflow node projection for run '{runId}' is invalid."));
                }
            }

            var edges = new List<WorkflowGraphEdge>(edgeLimit + 1);
            using (var command = con.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT " + WorkflowProjectionEdgeDbRow.SelectColumnList + """
                                       FROM workflow_projection_edges
                                       WHERE project_id = $1 AND run_id = $2 AND edge_id > $4
                                       ORDER BY edge_id
                                       LIMIT $3
                                       """;
                AddParameters(command, projectId, runId, edgeLimit + 1, edgeCursor ?? string.Empty);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var row = WorkflowProjectionEdgeDbRow.MapFromReader(reader);
                    edges.Add(JsonSerializer.Deserialize(
                                  row.EdgeJson,
                                  QylSerializerContext.Default.WorkflowGraphEdge)
                              ?? throw new InvalidDataException(
                                  $"Workflow edge projection for run '{runId}' is invalid."));
                }
            }

            var hasMoreNodes = nodes.Count > nodeLimit;
            var hasMoreEdges = edges.Count > edgeLimit;
            return new WorkflowGraphSnapshot
            {
                Run = state.Run,
                Nodes = nodes.Take(nodeLimit).ToArray(),
                Edges = edges.Take(edgeLimit).ToArray(),
                Statistics = state.Statistics,
                JournalSequence = state.JournalSequence,
                NextNodeCursor = hasMoreNodes ? nodes[nodeLimit - 1].NodeId : null,
                NextEdgeCursor = hasMoreEdges ? edges[edgeLimit - 1].EdgeId : null,
                HasMoreNodes = hasMoreNodes,
                HasMoreEdges = hasMoreEdges,
                TotalNodeCount = state.TotalNodeCount,
                TotalEdgeCount = state.TotalEdgeCount
            };
        }, ct);

    public Task RebuildWorkflowProjectionAsync(
        string projectId,
        string runId,
        CancellationToken ct = default) =>
        ExecuteMaintenanceWriteAsync(async (con, token) =>
        {
            await using var transaction = await con.BeginTransactionAsync(token).ConfigureAwait(false);
            var run = ReadWorkflowRun(con, projectId, runId, transaction)
                ?? throw new KeyNotFoundException($"Workflow run '{runId}' was not found.");
            PersistWorkflowProjection(
                con,
                transaction,
                run,
                ReadWorkflowEvents(con, projectId, runId, transaction));
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return 0;
        }, ct);

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

    public Task<WorkflowControlCommandStorageRow?> SubmitWorkflowControlAsync(
        string projectId,
        string runId,
        WorkflowControlAction action,
        string idempotencyKey,
        string? input,
        DateTimeOffset requestedAt,
        CancellationToken ct = default) =>
        ExecuteWriteAsync<WorkflowControlCommandStorageRow?>(async (con, token) =>
        {
            await using var transaction = await con.BeginTransactionAsync(token).ConfigureAwait(false);
            var run = ReadWorkflowRun(con, projectId, runId, transaction);
            if (run is null)
            {
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return null;
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
                return existing;
            }

            var commandId = $"cmd_{Guid.NewGuid():N}";
            var commandSequence = NextSequence(
                con,
                transaction,
                "SELECT nextval('workflow_command_sequence')");
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
                commandId,
                action,
                WorkflowControlStatus.Requested,
                requestedAt,
                null);
            var updatedRun = ReadWorkflowRun(con, projectId, runId, transaction)!;
            PersistWorkflowProjection(
                con,
                transaction,
                updatedRun,
                ReadWorkflowEvents(con, projectId, runId, transaction));
            var created = ReadControl(con, transaction, projectId, runId, commandId)!;
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return created;
        }, ct);

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

    public Task<WorkflowControlCommandStorageRow?> UpdateWorkflowControlAsync(
        string projectId,
        string runId,
        string commandId,
        WorkflowControlStatus status,
        string? error,
        DateTimeOffset updatedAt,
        CancellationToken ct = default) =>
        ExecuteWriteAsync<WorkflowControlCommandStorageRow?>(async (con, token) =>
        {
            await using var transaction = await con.BeginTransactionAsync(token).ConfigureAwait(false);
            var existing = ReadControl(con, transaction, projectId, runId, commandId);
            if (existing is null)
            {
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return null;
            }
            if (existing.Status == status)
            {
                if (existing.Error != error)
                    throw new WorkflowControlConflictException(
                        $"Control command '{commandId}' already has status '{ControlStatus(status)}' with different details.");
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return existing;
            }
            if (!IsControlTransitionAllowed(existing.Status, status))
            {
                throw new WorkflowControlConflictException(
                    $"Control command '{commandId}' cannot transition from '{ControlStatus(existing.Status)}' to '{ControlStatus(status)}'.");
            }

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

            var run = ReadWorkflowRun(con, projectId, runId, transaction)!;
            AppendControlJournalEvent(
                con,
                transaction,
                run,
                commandId,
                existing.Action,
                status,
                updatedAt,
                error);
            var updatedRun = ReadWorkflowRun(con, projectId, runId, transaction)!;
            PersistWorkflowProjection(
                con,
                transaction,
                updatedRun,
                ReadWorkflowEvents(con, projectId, runId, transaction));
            var updated = ReadControl(con, transaction, projectId, runId, commandId)!;
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return updated;
        }, ct);

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
            row.MetadataJson);
    }

    private static IReadOnlyList<WorkflowEventStorageRow> ReadWorkflowEvents(
        DuckDBConnection con,
        string projectId,
        string runId,
        DbTransaction? transaction = null)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT " + WorkflowEventDbRow.SelectColumnList + """
                               FROM workflow_events
                               WHERE project_id = $1 AND run_id = $2
                               ORDER BY journal_sequence
                               """;
        AddParameters(command, projectId, runId);
        using var reader = command.ExecuteReader();
        var rows = new List<WorkflowEventStorageRow>();
        while (reader.Read())
            rows.Add(ReadWorkflowEvent(reader));
        return rows;
    }

    private static WorkflowEventStorageRow ReadWorkflowEvent(DbDataReader reader)
    {
        var row = WorkflowEventDbRow.MapFromReader(reader);
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
        string clientId)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              SELECT source_sequence
                              FROM workflow_events
                              WHERE project_id = $1 AND run_id = $2 AND client_id = $3
                              ORDER BY source_sequence
                              """;
        AddParameters(command, projectId, runId, clientId);
        using var reader = command.ExecuteReader();
        ulong acknowledged = 0;
        while (reader.Read())
        {
            var sequence = DuckDbValueReader.ReadUInt64(reader, 0, 0);
            if (sequence == acknowledged + 1)
                acknowledged = sequence;
            else if (sequence > acknowledged + 1)
                break;
        }
        return acknowledged;
    }

    private static void PersistWorkflowProjection(
        DuckDBConnection con,
        DbTransaction transaction,
        WorkflowRunStorageRow run,
        IReadOnlyList<WorkflowEventStorageRow> events)
    {
        var projectionTime = run.EndedAt ??
                             (events.Count is 0 ? (DateTimeOffset?)null : events[^1].Timestamp) ??
                             run.StartedAt;
        var graph = WorkflowProjectionBuilder.Build(run, events, projectionTime);

        DeleteProjection(con, transaction, run.ProjectId, run.RunId);
        foreach (var node in graph.Nodes)
        {
            using var command = con.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = WorkflowProjectionNodeDbRow.BuildMultiRowInsertSql(1);
            WorkflowProjectionNodeDbRow.AddParameters(command, new WorkflowProjectionNodeDbRow
            {
                ProjectId = run.ProjectId,
                RunId = run.RunId,
                NodeId = node.NodeId,
                NodeJson = JsonSerializer.Serialize(node, QylSerializerContext.Default.WorkflowGraphNode)
            });
            command.ExecuteNonQuery();
        }
        foreach (var edge in graph.Edges)
        {
            using var command = con.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = WorkflowProjectionEdgeDbRow.BuildMultiRowInsertSql(1);
            WorkflowProjectionEdgeDbRow.AddParameters(command, new WorkflowProjectionEdgeDbRow
            {
                ProjectId = run.ProjectId,
                RunId = run.RunId,
                EdgeId = edge.EdgeId,
                EdgeJson = JsonSerializer.Serialize(edge, QylSerializerContext.Default.WorkflowGraphEdge)
            });
            command.ExecuteNonQuery();
        }
        using var state = con.CreateCommand();
        state.Transaction = transaction;
        state.CommandText = WorkflowProjectionStateDbRow.BuildMultiRowInsertSql(1);
        WorkflowProjectionStateDbRow.AddParameters(state, new WorkflowProjectionStateDbRow
        {
            ProjectId = run.ProjectId,
            RunId = run.RunId,
            JournalSequence = graph.JournalSequence,
            GraphJson = JsonSerializer.Serialize(
                new WorkflowProjectionState(
                    graph.Run,
                    graph.Statistics,
                    graph.JournalSequence,
                    graph.Nodes.Count,
                    graph.Edges.Count),
                WorkflowStorageJsonContext.Default.WorkflowProjectionState)
        });
        state.ExecuteNonQuery();
    }

    private static void DeleteProjection(
        DuckDBConnection con,
        DbTransaction transaction,
        string projectId,
        string runId)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              DELETE FROM workflow_projection_nodes
                              WHERE project_id = $1 AND run_id = $2;
                              DELETE FROM workflow_projection_edges
                              WHERE project_id = $1 AND run_id = $2;
                              DELETE FROM workflow_projection_state
                              WHERE project_id = $1 AND run_id = $2;
                              """;
        AddParameters(command, projectId, runId);
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

    private static void AppendControlJournalEvent(
        DuckDBConnection con,
        DbTransaction transaction,
        WorkflowRunStorageRow run,
        string commandId,
        WorkflowControlAction action,
        WorkflowControlStatus status,
        DateTimeOffset timestamp,
        string? error)
    {
        var sequence = NextSequence(
            con,
            transaction,
            "SELECT nextval('workflow_control_event_source_sequence')");
        var latest = run.LatestJournalSequence + 1;
        var statusText = ControlStatus(status);
        var errorJson = error is null
            ? "null"
            : JsonSerializer.Serialize(error, QylSerializerContext.Default.String);
        var dataJson =
            $"{{\"command_id\":{JsonSerializer.Serialize(commandId, QylSerializerContext.Default.String)},\"action\":{JsonSerializer.Serialize(ControlAction(action), QylSerializerContext.Default.String)},\"status\":{JsonSerializer.Serialize(statusText, QylSerializerContext.Default.String)},\"error\":{errorJson}}}";
        InsertWorkflowEvent(
            con,
            transaction,
            run.ProjectId,
            run.RunId,
            latest,
            "collector-control",
            new WorkflowEventWrite(
                $"control:{commandId}:{statusText}",
                sequence,
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
        using var update = con.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
                             UPDATE workflow_runs
                             SET latest_journal_sequence = $1, updated_at = current_timestamp
                             WHERE project_id = $2 AND run_id = $3
                             """;
        AddParameters(update, (decimal)latest, run.ProjectId, run.RunId);
        update.ExecuteNonQuery();
    }

    private static ulong NextSequence(
        DuckDBConnection con,
        DbTransaction transaction,
        string statement)
    {
        using var command = con.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = statement;
        return Convert.ToUInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
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
