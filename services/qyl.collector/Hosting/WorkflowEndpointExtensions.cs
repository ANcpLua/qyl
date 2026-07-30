using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Storage;
using Qyl.Collector.Workflow;

namespace Qyl.Collector.Hosting;

internal static partial class CollectorEndpointExtensions
{
    private const int DefaultRunLimit = 50;
    private const int DefaultEventLimit = 250;
    private const int DefaultCommandLimit = 20;
    private const int DefaultNodeLimit = 250;
    private const int DefaultEdgeLimit = 500;
    private const int MaximumAppendItems = 500;

    private const int MaxCursorLength = 192;
    private const int MaximumContentCharacters = 1_398_104;

    private static async Task<IResult> CreateRunAsync(
        HttpContext context,
        WorkflowRunCreateRequest request,
        IQylStore store,
        CancellationToken ct)
    {
        try
        {
            var row = await store.CreateWorkflowRunAsync(
                new WorkflowRunStorageRow(
                    ResolveProjectScope(context),
                    request.RunId,
                    request.ThreadId,
                    request.Title,
                    WorkflowRunStatus.Active,
                    request.StartedAt,
                    null,
                    0,
                    null,
                    SerializeObject(request.Metadata)),
                ct).ConfigureAwait(false);
            return Results.Ok(WorkflowProjectionBuilder.ToContract(row));
        }
        catch (WorkflowRunConflictException)
        {
            return ContractErrorResults.Conflict(
                request.RunId,
                "The run already exists with different immutable metadata.");
        }
        catch (QylStoreUnavailableException)
        {
            return ContractErrorResults.ServiceUnavailable("workflow_write_capacity");
        }
    }

    private static async Task<IResult> ListRunsAsync(
        HttpContext context,
        IQylStore store,
        CancellationToken ct)
    {
        if (!TryIntQuery(context, "limit", DefaultRunLimit, 1, 200, out var limit, out var error))
            return error!;
        if (!TryOffset(context.Request.Query["cursor"].FirstOrDefault(), out var offset))
            return ContractErrorResults.Validation("cursor", "Cursor must be a non-negative integer offset.",
                "cursor.invalid");
        if (!TryRunStatus(context.Request.Query["status"].FirstOrDefault(), out var status))
            return ContractErrorResults.Validation("status", "Unknown workflow run status.", "status.invalid");

        var rows = await store.ListWorkflowRunsAsync(
            ResolveProjectScope(context),
            status,
            limit + 1,
            offset,
            ct).ConfigureAwait(false);
        var hasMore = rows.Count > limit;
        return Results.Ok(new WorkflowRunPage
        {
            Items = rows.Take(limit).Select(WorkflowProjectionBuilder.ToContract).ToArray(),
            NextCursor = hasMore ? (offset + limit).ToString(CultureInfo.InvariantCulture) : null,
            HasMore = hasMore
        });
    }

    private static async Task<IResult> GetRunAsync(
        HttpContext context,
        [FromRoute(Name = "run_id")] string runId,
        IQylStore store,
        CancellationToken ct)
    {
        var row = await store.GetWorkflowRunAsync(ResolveProjectScope(context), runId, ct).ConfigureAwait(false);
        return row is null
            ? ContractErrorResults.NotFound("workflow_run", runId)
            : Results.Ok(WorkflowProjectionBuilder.ToContract(row));
    }

    internal static async Task<IResult> AppendEventsAsync(
        HttpContext context,
        [FromRoute(Name = "run_id")] string runId,
        WorkflowEventBatchAppendRequest request,
        IQylStore store,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || request.ClientId.Length > 128)
        {
            return ContractErrorResults.Validation(
                "client_id",
                "client_id must contain between 1 and 128 characters.",
                "client_id.invalid");
        }
        if (request.ClientId == "collector-control")
        {
            return ContractErrorResults.Validation(
                "client_id",
                "client_id is reserved for collector control events.",
                "client_id.reserved");
        }
        if (request.Events.Count is < 1 or > MaximumAppendItems)
        {
            return ContractErrorResults.Validation(
                "events",
                $"events must contain between 1 and {MaximumAppendItems} items.",
                "events.out_of_range");
        }
        if (request.Content?.Count > MaximumAppendItems)
        {
            return ContractErrorResults.Validation(
                "content",
                $"content must contain at most {MaximumAppendItems} items.",
                "content.out_of_range");
        }
        if (request.Content?.Any(static chunk =>
                chunk.Content.Length > MaximumContentCharacters) is true)
        {
            return ContractErrorResults.Validation(
                "content",
                $"Each content chunk must contain at most {MaximumContentCharacters} characters.",
                "content.too_large");
        }
        if (request.Events.Any(static workflowEvent => workflowEvent.ContentRefs?.Count > 64))
        {
            return ContractErrorResults.Validation(
                "content_refs",
                "Each event may reference at most 64 content chunks.",
                "content_refs.out_of_range");
        }

        try
        {
            var result = await store.AppendWorkflowEventsAsync(
                ResolveProjectScope(context),
                runId,
                request.ClientId,
                request.Events.Select(static workflowEvent => new WorkflowEventWrite(
                    workflowEvent.EventId,
                    workflowEvent.SourceSequence,
                    workflowEvent.Timestamp,
                    workflowEvent.Kind,
                    workflowEvent.ThreadId,
                    workflowEvent.TurnId,
                    workflowEvent.AttemptId,
                    workflowEvent.AgentId,
                    workflowEvent.ParentAgentId,
                    workflowEvent.ReceiverAgentId,
                    workflowEvent.ToolCallId,
                    workflowEvent.ContentRefs ?? [],
                    SerializeObject(workflowEvent.Data))).ToArray(),
                request.Content?.Select(static content => new WorkflowContentWrite(
                    content.ContentRef,
                    content.ContentType,
                    content.Encoding,
                    content.Content)).ToArray() ?? [],
                ct).ConfigureAwait(false);
            return Results.Ok(new WorkflowEventBatchAppendResponse
            {
                AcceptedCount = result.AcceptedCount,
                DuplicateCount = result.DuplicateCount,
                AcknowledgedSourceSequence = result.AcknowledgedSourceSequence,
                FirstJournalSequence = result.FirstJournalSequence,
                LastJournalSequence = result.LastJournalSequence
            });
        }
        catch (KeyNotFoundException)
        {
            return ContractErrorResults.NotFound("workflow_run", runId);
        }
        catch (WorkflowContentValidationException)
        {
            return ContractErrorResults.Validation(
                "content",
                "Base64 workflow content must use valid base64 encoding.",
                "content.base64.invalid");
        }
        catch (WorkflowProjectionLimitExceededException)
        {
            return ContractErrorResults.Conflict(
                runId,
                "The workflow run has reached its immutable journal or projection capacity.");
        }
        catch (Exception error) when (error is WorkflowEventConflictException or InvalidDataException)
        {
            return ContractErrorResults.Conflict(
                runId,
                "The event batch conflicts with the immutable workflow journal.");
        }
        catch (QylStoreUnavailableException)
        {
            return ContractErrorResults.ServiceUnavailable("workflow_write_capacity");
        }
    }

    private static async Task<IResult> ReadEventsAsync(
        HttpContext context,
        [FromRoute(Name = "run_id")] string runId,
        IQylStore store,
        CancellationToken ct)
    {
        if (!TryUlongQuery(context, "after_sequence", 0, out var after, out var error))
            return error!;
        if (!TryIntQuery(context, "limit", DefaultEventLimit, 1, 1000, out var limit, out error))
            return error!;
        if (!TryIntQuery(context, "wait_ms", 0, 0, 30000, out var waitMs, out error))
            return error!;

        var deadline = TimeProvider.System.GetUtcNow().AddMilliseconds(waitMs);
        while (true)
        {
            var page = await store.ReadWorkflowEventsAsync(
                ResolveProjectScope(context),
                runId,
                after,
                limit,
                ct).ConfigureAwait(false);
            if (page is null)
                return ContractErrorResults.NotFound("workflow_run", runId);
            if (page.Events.Count > 0 || waitMs is 0 || TimeProvider.System.GetUtcNow() >= deadline)
                return Results.Ok(ToContract(page));
            await Task.Delay(TimeSpan.FromMilliseconds(250), TimeProvider.System, ct).ConfigureAwait(false);
        }
    }

    internal static async Task<IResult> GetGraphAsync(
        HttpContext context,
        [FromRoute(Name = "run_id")] string runId,
        IQylStore store,
        CancellationToken ct)
    {
        if (!TryCursor(context.Request.Query["node_cursor"].FirstOrDefault(), out var nodeCursor))
        {
            return ContractErrorResults.Validation(
                "node_cursor",
                "node_cursor must be a cursor previously returned as next_node_cursor.",
                "node_cursor.invalid");
        }
        if (!TryCursor(context.Request.Query["edge_cursor"].FirstOrDefault(), out var edgeCursor))
        {
            return ContractErrorResults.Validation(
                "edge_cursor",
                "edge_cursor must be a cursor previously returned as next_edge_cursor.",
                "edge_cursor.invalid");
        }
        if (!TryIntQuery(
                context,
                "node_limit",
                DefaultNodeLimit,
                1,
                1000,
                out var nodeLimit,
                out var error))
        {
            return error!;
        }
        if (!TryIntQuery(
                context,
                "edge_limit",
                DefaultEdgeLimit,
                1,
                2000,
                out var edgeLimit,
                out error))
        {
            return error!;
        }

        try
        {
            var graph = await store.GetWorkflowGraphAsync(
                ResolveProjectScope(context),
                runId,
                nodeCursor,
                nodeLimit,
                edgeCursor,
                edgeLimit,
                ct).ConfigureAwait(false);
            return graph is null
                ? ContractErrorResults.NotFound("workflow_run", runId)
                : Results.Ok(graph);
        }
        catch (KeyNotFoundException)
        {
            return ContractErrorResults.NotFound("workflow_run", runId);
        }
        catch (WorkflowProjectionLimitExceededException)
        {
            return ContractErrorResults.Conflict(
                runId,
                "The workflow run has reached its immutable journal or projection capacity.");
        }
        catch (QylStoreUnavailableException)
        {
            return ContractErrorResults.ServiceUnavailable(
                "workflow_projection_capacity");
        }
    }

    private static async Task<IResult> GetContentAsync(
        HttpContext context,
        [FromRoute(Name = "run_id")] string runId,
        [FromRoute(Name = "content_ref")] string contentRef,
        IQylStore store,
        CancellationToken ct)
    {
        var content = await store.GetWorkflowContentAsync(
            ResolveProjectScope(context),
            runId,
            contentRef,
            ct).ConfigureAwait(false);
        return content is null
            ? ContractErrorResults.NotFound("workflow_content", contentRef)
            : Results.Ok(new WorkflowContent
            {
                ContentRef = content.ContentRef,
                ContentType = content.ContentType,
                Encoding = content.Encoding,
                Content = content.Content,
                SizeBytes = content.SizeBytes
            });
    }

    private static async Task<IResult> StreamEventsAsync(
        HttpContext context,
        [FromRoute(Name = "run_id")] string runId,
        IQylStore store,
        CancellationToken ct)
    {
        var lastEventId = context.Request.Headers["Last-Event-ID"].FirstOrDefault();
        if (lastEventId is not null &&
            !ulong.TryParse(
                lastEventId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out _))
        {
            return ContractErrorResults.Validation(
                "Last-Event-ID",
                "Last-Event-ID must be an unsigned journal sequence.",
                "last_event_id.invalid",
                lastEventId);
        }
        var cursor = lastEventId is null
            ? 0
            : ulong.Parse(lastEventId, NumberStyles.None, CultureInfo.InvariantCulture);
        var page = await store.ReadWorkflowEventsAsync(
            ResolveProjectScope(context),
            runId,
            cursor,
            DefaultEventLimit,
            ct).ConfigureAwait(false);
        if (page is null)
            return ContractErrorResults.NotFound("workflow_run", runId);

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        while (!ct.IsCancellationRequested)
        {
            if (page.CursorGap)
            {
                var oldest = page.Events.Count is 0
                    ? page.HighWaterMark
                    : page.Events[0].JournalSequence;
                await WriteSseEventAsync(
                    context.Response,
                    "cursor_gap",
                    null,
                    new WorkflowCursorGapEvent
                    {
                        Type = "cursor_gap",
                        OldestAvailableSequence = oldest,
                        HighWaterMark = page.HighWaterMark,
                        Timestamp = TimeProvider.System.GetUtcNow()
                    },
                    QylSerializerContext.Default.WorkflowCursorGapEvent,
                    ct).ConfigureAwait(false);
                cursor = oldest > 0 ? oldest - 1 : 0;
            }
            else if (page.Events.Count > 0)
            {
                foreach (var workflowEvent in page.Events)
                {
                    var contract = ToContract(workflowEvent);
                    await WriteSseEventAsync(
                        context.Response,
                        "event",
                        workflowEvent.JournalSequence,
                        contract,
                        QylSerializerContext.Default.WorkflowJournalEvent,
                        ct).ConfigureAwait(false);
                    cursor = workflowEvent.JournalSequence;
                }
            }
            else
            {
                await WriteSseEventAsync(
                    context.Response,
                    "heartbeat",
                    null,
                    new WorkflowHeartbeatEvent
                    {
                        Type = "heartbeat",
                        HighWaterMark = page.HighWaterMark,
                        Timestamp = TimeProvider.System.GetUtcNow()
                    },
                    QylSerializerContext.Default.WorkflowHeartbeatEvent,
                    ct).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(1), TimeProvider.System, ct).ConfigureAwait(false);
            }

            page = await store.ReadWorkflowEventsAsync(
                ResolveProjectScope(context),
                runId,
                cursor,
                DefaultEventLimit,
                ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Workflow run '{runId}' disappeared while its event stream was active.");
        }

        return Results.Empty;
    }

    private static async Task<IResult> SubmitControlAsync(
        HttpContext context,
        [FromRoute(Name = "run_id")] string runId,
        WorkflowControlRequest request,
        IQylStore store,
        CancellationToken ct)
    {
        if (request.Action is WorkflowControlAction.Steer or WorkflowControlAction.Resume &&
            string.IsNullOrWhiteSpace(request.Input))
        {
            return ContractErrorResults.Validation(
                "input",
                "Steer and resume require non-empty input.",
                "control.input_required");
        }
        if (request.Input?.Length > 32768)
        {
            return ContractErrorResults.Validation(
                "input",
                "Control input must contain at most 32768 characters.",
                "control.input_too_large");
        }
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) ||
            request.IdempotencyKey.Length > 160)
        {
            return ContractErrorResults.Validation(
                "idempotency_key",
                "idempotency_key must contain between 1 and 160 characters.",
                "control.idempotency_key_invalid");
        }
        try
        {
            var command = await store.SubmitWorkflowControlAsync(
                ResolveProjectScope(context),
                runId,
                request.Action,
                request.IdempotencyKey,
                request.Input,
                TimeProvider.System.GetUtcNow(),
                ct).ConfigureAwait(false);
            return command is null
                ? ContractErrorResults.NotFound("workflow_run", runId)
                : Results.Ok(ToContract(command));
        }
        catch (WorkflowControlConflictException)
        {
            return ContractErrorResults.Conflict(
                runId,
                "The idempotency key is already bound to a different control command.");
        }
        catch (WorkflowProjectionLimitExceededException)
        {
            return ContractErrorResults.Conflict(
                runId,
                "The workflow run has reached its immutable journal or projection capacity.");
        }
        catch (QylStoreUnavailableException)
        {
            return ContractErrorResults.ServiceUnavailable("workflow_control_capacity");
        }
    }

    private static async Task<IResult> PollControlsAsync(
        HttpContext context,
        [FromRoute(Name = "run_id")] string runId,
        IQylStore store,
        CancellationToken ct)
    {
        if (!TryUlongQuery(context, "after_sequence", 0, out var after, out var error))
            return error!;
        if (!TryIntQuery(context, "limit", DefaultCommandLimit, 1, 100, out var limit, out error))
            return error!;
        if (!TryIntQuery(context, "wait_ms", 0, 0, 30000, out var waitMs, out error))
            return error!;

        var deadline = TimeProvider.System.GetUtcNow().AddMilliseconds(waitMs);
        while (true)
        {
            var page = await store.PollWorkflowControlsAsync(
                ResolveProjectScope(context),
                runId,
                after,
                limit,
                ct).ConfigureAwait(false);
            if (page is null)
                return ContractErrorResults.NotFound("workflow_run", runId);
            if (page.Commands.Count > 0 || waitMs is 0 || TimeProvider.System.GetUtcNow() >= deadline)
            {
                return Results.Ok(new WorkflowControlCommandPage
                {
                    Commands = page.Commands.Select(ToContract).ToArray(),
                    NextSequence = page.NextSequence
                });
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250), TimeProvider.System, ct).ConfigureAwait(false);
        }
    }

    internal static async Task<IResult> UpdateControlAsync(
        HttpContext context,
        [FromRoute(Name = "run_id")] string runId,
        [FromRoute(Name = "command_id")] string commandId,
        WorkflowControlStatusUpdateRequest request,
        IQylStore store,
        CancellationToken ct)
    {
        try
        {
            var command = await store.UpdateWorkflowControlAsync(
                ResolveProjectScope(context),
                runId,
                commandId,
                request.Status,
                request.Error,
                // The journal event minted for this transition carries the adapter's clock
                // when the adapter reported one — the journal is otherwise agent-clocked,
                // and a collector receipt time here measured HTTP latency, not work. The
                // receipt time remains the fallback for callers that omit it.
                request.OccurredAt ?? TimeProvider.System.GetUtcNow(),
                ct).ConfigureAwait(false);
            return command is null
                ? ContractErrorResults.NotFound("workflow_control", commandId)
                : Results.Ok(ToContract(command));
        }
        catch (WorkflowControlConflictException)
        {
            return ContractErrorResults.Conflict(
                commandId,
                "The control command status transition conflicts with the recorded command.");
        }
        catch (WorkflowProjectionLimitExceededException)
        {
            return ContractErrorResults.Conflict(
                runId,
                "The workflow run has reached its immutable journal or projection capacity.");
        }
        catch (QylStoreUnavailableException)
        {
            return ContractErrorResults.ServiceUnavailable(
                "workflow_control_capacity");
        }
    }

    private static WorkflowEventPage ToContract(WorkflowEventStoragePage page) =>
        new()
        {
            Events = page.Events.Select(ToContract).ToArray(),
            NextSequence = page.NextSequence,
            HighWaterMark = page.HighWaterMark,
            CursorGap = page.CursorGap
        };

    private static WorkflowJournalEvent ToContract(WorkflowEventStorageRow workflowEvent) =>
        new()
        {
            EventId = workflowEvent.EventId,
            SourceSequence = workflowEvent.SourceSequence,
            Timestamp = workflowEvent.Timestamp,
            Kind = workflowEvent.Kind,
            ThreadId = workflowEvent.ThreadId,
            TurnId = workflowEvent.TurnId,
            AttemptId = workflowEvent.AttemptId,
            AgentId = workflowEvent.AgentId,
            ParentAgentId = workflowEvent.ParentAgentId,
            ReceiverAgentId = workflowEvent.ReceiverAgentId,
            ToolCallId = workflowEvent.ToolCallId,
            ContentRefs = workflowEvent.ContentRefs.Count is 0 ? null : workflowEvent.ContentRefs,
            Data = ParseObject(workflowEvent.DataJson),
            RunId = workflowEvent.RunId,
            ClientId = workflowEvent.ClientId,
            JournalSequence = workflowEvent.JournalSequence
        };

    private static WorkflowControlCommand ToContract(WorkflowControlCommandStorageRow command) =>
        new()
        {
            CommandId = command.CommandId,
            RunId = command.RunId,
            Action = command.Action,
            Status = command.Status,
            IdempotencyKey = command.IdempotencyKey,
            Input = command.Input,
            RequestedAt = command.RequestedAt,
            UpdatedAt = command.UpdatedAt,
            CommandSequence = command.CommandSequence,
            Error = command.Error
        };

    private static string? SerializeObject(IReadOnlyDictionary<string, object>? value) =>
        value is null
            ? null
            : JsonSerializer.Serialize(
                value.ToDictionary(static pair => pair.Key, static pair => (object?)pair.Value),
                QylSerializerContext.Default.DictionaryStringObject);

    private static IReadOnlyDictionary<string, object>? ParseObject(string? json)
    {
        if (json is null)
            return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject()
            .ToDictionary(
                static property => property.Name,
                static property => (object)property.Value.Clone(),
                StringComparer.Ordinal);
    }

    private static bool TryIntQuery(
        HttpContext context,
        string name,
        int defaultValue,
        int minimum,
        int maximum,
        out int value,
        out IResult? error)
    {
        var raw = context.Request.Query[name].FirstOrDefault();
        if (raw is null)
        {
            value = defaultValue;
            error = null;
            return true;
        }
        if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
            value >= minimum && value <= maximum)
        {
            error = null;
            return true;
        }
        error = ContractErrorResults.Validation(
            name,
            $"{name} must be between {minimum} and {maximum}.",
            $"{name}.out_of_range",
            raw);
        return false;
    }

    private static bool TryUlongQuery(
        HttpContext context,
        string name,
        ulong defaultValue,
        out ulong value,
        out IResult? error)
    {
        var raw = context.Request.Query[name].FirstOrDefault();
        if (raw is null)
        {
            value = defaultValue;
            error = null;
            return true;
        }
        if (ulong.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            error = null;
            return true;
        }
        error = ContractErrorResults.Validation(name, $"{name} must be an unsigned integer.",
            $"{name}.invalid", raw);
        return false;
    }

    private static bool TryCursor(string? raw, out string? cursor)
    {
        cursor = string.IsNullOrEmpty(raw) ? null : raw;
        return cursor is null || cursor.EnumerateRunes().Count() <= MaxCursorLength;
    }

    private static bool TryOffset(string? raw, out int offset)
    {
        offset = 0;
        return raw is null ||
               int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out offset) && offset >= 0;
    }

    private static bool TryRunStatus(string? raw, out WorkflowRunStatus? status)
    {
        status = raw switch
        {
            null => null,
            "active" => WorkflowRunStatus.Active,
            "completed" => WorkflowRunStatus.Completed,
            "failed" => WorkflowRunStatus.Failed,
            "interrupted" => WorkflowRunStatus.Interrupted,
            _ => null
        };
        return raw is null || status.HasValue;
    }

    private static async Task WriteSseEventAsync<T>(
        HttpResponse response,
        string eventType,
        ulong? eventId,
        T value,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken ct)
    {
        if (eventId.HasValue)
        {
            await response.WriteAsync(
                $"id: {eventId.Value.ToString(CultureInfo.InvariantCulture)}\n",
                ct).ConfigureAwait(false);
        }
        await response.WriteAsync($"event: {eventType}\ndata: ", ct).ConfigureAwait(false);
        await JsonSerializer.SerializeAsync(response.Body, value, jsonTypeInfo, ct).ConfigureAwait(false);
        await response.WriteAsync("\n\n", ct).ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }
}
