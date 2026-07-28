using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Qyl.Api.Contracts.Workflow;

namespace Qyl.Cli.Codex;

internal sealed class CodexEventNormalizer
{
    private readonly HashSet<string> _eventIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ThreadContext> _threads = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ApprovalContext> _approvals = new(StringComparer.Ordinal);
    private ulong _sourceSequence;
    private string? _rootThreadId;
    private string? _activeRootTurnId;
    private string? _lastRootTurnStatus;

    public string? RootThreadId => _rootThreadId;
    public string? RootTitle { get; private set; }

    public CodexControlTarget ControlTarget =>
        new(_rootThreadId, _activeRootTurnId);

    public CodexNormalizedBatch StartRun(DateTimeOffset timestamp)
    {
        var workflowEvent = CreateEvent(
            StableEventId("run", "created"),
            WorkflowJournalEventKind.RunCreated,
            timestamp,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            Data(("status", "active"), ("capture", "full")));
        return workflowEvent is null
            ? default
            : new CodexNormalizedBatch([workflowEvent], []);
    }

    public CodexNormalizedBatch Normalize(JsonElement message, DateTimeOffset receivedAt)
    {
        if (!message.TryGetProperty("method", out var methodElement) ||
            methodElement.ValueKind is not JsonValueKind.String)
        {
            return default;
        }

        var method = methodElement.GetString()
            ?? throw new InvalidDataException("Codex app-server sent a null method name.");
        var parameters = message.TryGetProperty("params", out var paramsElement)
            ? paramsElement
            : default;
        var events = new List<WorkflowEventAppend>();
        var content = new Dictionary<string, WorkflowContentChunk>(StringComparer.Ordinal);

        switch (method)
        {
            case "thread/started":
                NormalizeThreadStarted(parameters, receivedAt, events);
                break;
            case "turn/started":
                NormalizeTurnStarted(parameters, receivedAt, events);
                break;
            case "turn/completed":
                NormalizeTurnCompleted(parameters, receivedAt, events);
                break;
            case "item/started":
                NormalizeItem(parameters, receivedAt, completed: false, events, content);
                break;
            case "item/completed":
                NormalizeItem(parameters, receivedAt, completed: true, events, content);
                break;
            case "thread/status/changed":
                NormalizeThreadStatus(parameters, receivedAt, events);
                break;
            case "serverRequest/resolved":
                NormalizeApprovalResolved(parameters, receivedAt, events);
                break;
            case "item/commandExecution/requestApproval":
            case "item/fileChange/requestApproval":
            case "item/permissions/requestApproval":
                NormalizeApprovalRequested(message, method, parameters, receivedAt, events, content);
                break;
            case "item/autoApprovalReview/started":
                NormalizeAutoApproval(parameters, receivedAt, completed: false, events, content);
                break;
            case "item/autoApprovalReview/completed":
                NormalizeAutoApproval(parameters, receivedAt, completed: true, events, content);
                break;
        }

        return new CodexNormalizedBatch(events, content.Values.ToArray());
    }

    public CodexNormalizedBatch CompleteRun(DateTimeOffset timestamp, bool succeeded)
    {
        var status = succeeded && _lastRootTurnStatus is not "failed"
            ? "completed"
            : "failed";
        var workflowEvent = CreateEvent(
            StableEventId("run", "completed", _rootThreadId ?? "unknown"),
            WorkflowJournalEventKind.RunCompleted,
            timestamp,
            _rootThreadId,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            Data(("status", status)));
        return workflowEvent is null
            ? default
            : new CodexNormalizedBatch([workflowEvent], []);
    }

    private void NormalizeThreadStarted(
        JsonElement parameters,
        DateTimeOffset receivedAt,
        List<WorkflowEventAppend> events)
    {
        if (!TryProperty(parameters, "thread", out var thread) ||
            !TryString(thread, "id", out var threadId))
        {
            return;
        }

        var parentThreadId = OptionalString(thread, "parentThreadId");
        var timestamp = UnixSeconds(thread, "createdAt") ?? receivedAt;
        if (parentThreadId is null)
        {
            if (_rootThreadId is not null && _rootThreadId != threadId)
                return;
            _rootThreadId = threadId;
            RootTitle = OptionalString(thread, "name") ?? OptionalString(thread, "preview");
            _threads[threadId] = new ThreadContext(null, null, null);
            Add(
                events,
                StableEventId("thread", threadId, "started"),
                WorkflowJournalEventKind.ThreadStarted,
                timestamp,
                threadId,
                null,
                null,
                null,
                null,
                null,
                null,
                [],
                Data(("label", RootTitle)));
            return;
        }

        if (!IsObservedThread(parentThreadId))
            return;
        var parent = _threads.GetValueOrDefault(parentThreadId);
        var attemptId = parent?.AttemptId ?? CurrentAttemptId(parentThreadId);
        _threads[threadId] = new ThreadContext(parentThreadId, attemptId, null);
        Add(
            events,
            StableEventId("agent", threadId, "started"),
            WorkflowJournalEventKind.AgentStarted,
            timestamp,
            threadId,
            null,
            attemptId,
            threadId,
            AgentId(parentThreadId),
            null,
            null,
            [],
            Data(
                ("label", OptionalString(thread, "agentNickname") ?? OptionalString(thread, "agentRole") ?? threadId),
                ("status", "running")));
    }

    private void NormalizeTurnStarted(
        JsonElement parameters,
        DateTimeOffset receivedAt,
        List<WorkflowEventAppend> events)
    {
        if (!TryString(parameters, "threadId", out var threadId) ||
            !TryProperty(parameters, "turn", out var turn) ||
            !TryString(turn, "id", out var turnId) ||
            !IsObservedThread(threadId))
        {
            return;
        }

        var timestamp = UnixSeconds(turn, "startedAt") ?? receivedAt;
        string? attemptId;
        if (threadId == _rootThreadId)
        {
            attemptId = turnId;
            _activeRootTurnId = turnId;
            _threads[threadId] = _threads[threadId] with { AttemptId = attemptId, ActiveTurnId = turnId };
            Add(
                events,
                StableEventId("attempt", turnId, "started"),
                WorkflowJournalEventKind.AttemptStarted,
                timestamp,
                threadId,
                turnId,
                attemptId,
                null,
                null,
                null,
                null,
                [],
                Data(("label", $"Attempt {turnId}"), ("status", "running")));
        }
        else
        {
            attemptId = CurrentAttemptId(threadId);
            var context = _threads[threadId];
            _threads[threadId] = context with { ActiveTurnId = turnId };
        }

        Add(
            events,
            StableEventId("turn", threadId, turnId, "started"),
            WorkflowJournalEventKind.TurnStarted,
            timestamp,
            threadId,
            turnId,
            attemptId,
            AgentId(threadId),
            ParentAgentId(threadId),
            null,
            null,
            [],
            Data(("label", $"Turn {turnId}"), ("status", "running")));
    }

    private void NormalizeTurnCompleted(
        JsonElement parameters,
        DateTimeOffset receivedAt,
        List<WorkflowEventAppend> events)
    {
        if (!TryString(parameters, "threadId", out var threadId) ||
            !TryProperty(parameters, "turn", out var turn) ||
            !TryString(turn, "id", out var turnId) ||
            !IsObservedThread(threadId))
        {
            return;
        }

        var timestamp = UnixSeconds(turn, "completedAt") ?? receivedAt;
        var status = OptionalString(turn, "status") ?? "completed";
        var attemptId = threadId == _rootThreadId ? turnId : CurrentAttemptId(threadId);
        var kind = status == "interrupted"
            ? WorkflowJournalEventKind.TurnInterrupted
            : WorkflowJournalEventKind.TurnCompleted;
        Add(
            events,
            StableEventId("turn", threadId, turnId, "completed"),
            kind,
            timestamp,
            threadId,
            turnId,
            attemptId,
            AgentId(threadId),
            ParentAgentId(threadId),
            null,
            null,
            [],
            Data(("status", NormalizeStatus(status))));

        if (threadId == _rootThreadId)
        {
            _activeRootTurnId = null;
            _lastRootTurnStatus = status;
            _threads[threadId] = _threads[threadId] with { ActiveTurnId = null };
            Add(
                events,
                StableEventId("attempt", turnId, "completed"),
                WorkflowJournalEventKind.AttemptCompleted,
                timestamp,
                threadId,
                turnId,
                attemptId,
                null,
                null,
                null,
                null,
                [],
                Data(("status", NormalizeStatus(status))));
        }
        else
        {
            var context = _threads[threadId];
            _threads[threadId] = context with { ActiveTurnId = null };
            Add(
                events,
                StableEventId("agent", threadId, turnId, "completed"),
                WorkflowJournalEventKind.AgentCompleted,
                timestamp,
                threadId,
                turnId,
                attemptId,
                threadId,
                ParentAgentId(threadId),
                null,
                null,
                [],
                Data(("status", NormalizeStatus(status))));
        }
    }

    private void NormalizeItem(
        JsonElement parameters,
        DateTimeOffset receivedAt,
        bool completed,
        List<WorkflowEventAppend> events,
        Dictionary<string, WorkflowContentChunk> content)
    {
        if (!TryString(parameters, "threadId", out var threadId) ||
            !TryString(parameters, "turnId", out var turnId) ||
            !TryProperty(parameters, "item", out var item) ||
            !TryString(item, "id", out var itemId) ||
            !TryString(item, "type", out var itemType) ||
            !IsObservedThread(threadId))
        {
            return;
        }

        var timestamp = UnixMilliseconds(
            parameters,
            completed ? "completedAtMs" : "startedAtMs") ?? receivedAt;
        var chunk = Capture(item);
        content.TryAdd(chunk.ContentRef, chunk);
        var contentRefs = new[] { chunk.ContentRef };
        var attemptId = CurrentAttemptId(threadId);
        var agentId = AgentId(threadId);
        Add(
            events,
            StableEventId("item", threadId, turnId, itemId, completed ? "completed" : "started"),
            completed ? WorkflowJournalEventKind.ItemCompleted : WorkflowJournalEventKind.ItemStarted,
            timestamp,
            threadId,
            turnId,
            attemptId,
            agentId,
            ParentAgentId(threadId),
            null,
            null,
            contentRefs,
            Data(
                ("item_id", itemId),
                ("label", ItemLabel(item)),
                ("status", completed ? ItemStatus(item) : "running")));

        switch (itemType)
        {
            case "collabAgentToolCall":
                NormalizeCollaboration(
                    item,
                    threadId,
                    turnId,
                    attemptId,
                    timestamp,
                    completed,
                    contentRefs,
                    events);
                break;
            case "subAgentActivity":
                NormalizeSubAgentActivity(
                    item,
                    threadId,
                    turnId,
                    attemptId,
                    timestamp,
                    contentRefs,
                    events);
                break;
            case "commandExecution":
            case "fileChange":
            case "mcpToolCall":
            case "dynamicToolCall":
            case "webSearch":
            case "imageView":
            case "imageGeneration":
            case "sleep":
                NormalizeTool(
                    item,
                    itemType,
                    threadId,
                    turnId,
                    attemptId,
                    timestamp,
                    completed,
                    contentRefs,
                    events);
                break;
        }
    }

    private void NormalizeCollaboration(
        JsonElement item,
        string threadId,
        string turnId,
        string? attemptId,
        DateTimeOffset timestamp,
        bool completed,
        IReadOnlyList<string> contentRefs,
        List<WorkflowEventAppend> events)
    {
        var itemId = RequiredString(item, "id");
        var tool = OptionalString(item, "tool") ?? "unknown";
        var sender = OptionalString(item, "senderThreadId") ?? threadId;
        var receivers = StringArray(item, "receiverThreadIds");
        var status = OptionalString(item, "status") ?? (completed ? "completed" : "inProgress");

        if (tool == "spawnAgent" && completed)
        {
            foreach (var receiver in receivers)
            {
                _threads.TryAdd(receiver, new ThreadContext(sender, attemptId, null));
                Add(
                    events,
                    StableEventId("collab", itemId, "spawn", receiver),
                    WorkflowJournalEventKind.AgentSpawned,
                    timestamp,
                    threadId,
                    turnId,
                    attemptId,
                    receiver,
                    AgentId(sender),
                    null,
                    itemId,
                    contentRefs,
                    Data(
                        ("label", AgentLabel(item, receiver)),
                        ("status", status == "failed" ? "failed" : "pending"),
                        ("operation", "spawn")));
            }
            return;
        }

        if (tool is "sendInput" or "resumeAgent" && completed)
        {
            foreach (var receiver in receivers)
            {
                Add(
                    events,
                    StableEventId("collab", itemId, "message", receiver),
                    WorkflowJournalEventKind.MessageSent,
                    timestamp,
                    threadId,
                    turnId,
                    attemptId,
                    AgentId(sender),
                    ParentAgentId(sender),
                    receiver,
                    itemId,
                    contentRefs,
                    Data(("label", tool == "resumeAgent" ? "Resume agent" : "Send input")));
            }
            return;
        }

        if (tool == "wait")
        {
            Add(
                events,
                StableEventId("collab", itemId, completed ? "wait-completed" : "wait-started"),
                completed ? WorkflowJournalEventKind.WaitCompleted : WorkflowJournalEventKind.WaitStarted,
                timestamp,
                threadId,
                turnId,
                attemptId,
                AgentId(sender),
                ParentAgentId(sender),
                null,
                itemId,
                contentRefs,
                Data(("wait_id", itemId), ("label", "Wait for agents"), ("status", NormalizeStatus(status))));
            if (completed)
            {
                foreach (var receiver in receivers)
                {
                    Add(
                        events,
                        StableEventId("collab", itemId, "joined", receiver),
                        WorkflowJournalEventKind.Joined,
                        timestamp,
                        threadId,
                        turnId,
                        attemptId,
                        AgentId(sender),
                        ParentAgentId(sender),
                        receiver,
                        itemId,
                        contentRefs,
                        Data(("wait_id", itemId), ("label", "Agent joined")));
                }
            }
            return;
        }

        if (tool == "closeAgent" && completed)
        {
            foreach (var receiver in receivers)
            {
                Add(
                    events,
                    StableEventId("collab", itemId, "closed", receiver),
                    WorkflowJournalEventKind.AgentCompleted,
                    timestamp,
                    threadId,
                    turnId,
                    attemptId,
                    receiver,
                    AgentId(sender),
                    null,
                    itemId,
                    contentRefs,
                    Data(("status", NormalizeStatus(status))));
            }
        }
    }

    private void NormalizeSubAgentActivity(
        JsonElement item,
        string threadId,
        string turnId,
        string? attemptId,
        DateTimeOffset timestamp,
        IReadOnlyList<string> contentRefs,
        List<WorkflowEventAppend> events)
    {
        if (!TryString(item, "agentThreadId", out var agentThreadId))
            return;
        var kind = OptionalString(item, "kind");
        if (kind is not ("started" or "interrupted"))
            return;
        _threads.TryAdd(agentThreadId, new ThreadContext(threadId, attemptId, null));
        Add(
            events,
            StableEventId("subagent-activity", RequiredString(item, "id"), kind),
            kind == "started"
                ? WorkflowJournalEventKind.AgentStarted
                : WorkflowJournalEventKind.AgentCompleted,
            timestamp,
            threadId,
            turnId,
            attemptId,
            agentThreadId,
            AgentId(threadId),
            null,
            null,
            contentRefs,
            Data(
                ("label", OptionalString(item, "agentPath") ?? agentThreadId),
                ("status", kind == "started" ? "running" : "interrupted")));
    }

    private void NormalizeTool(
        JsonElement item,
        string itemType,
        string threadId,
        string turnId,
        string? attemptId,
        DateTimeOffset timestamp,
        bool completed,
        IReadOnlyList<string> contentRefs,
        List<WorkflowEventAppend> events)
    {
        var itemId = RequiredString(item, "id");
        var status = completed ? ItemStatus(item) : "running";
        Add(
            events,
            StableEventId("tool", threadId, turnId, itemId, completed ? "completed" : "started"),
            completed ? WorkflowJournalEventKind.ToolCompleted : WorkflowJournalEventKind.ToolStarted,
            timestamp,
            threadId,
            turnId,
            attemptId,
            AgentId(threadId),
            ParentAgentId(threadId),
            null,
            itemId,
            contentRefs,
            Data(("tool_name", ToolLabel(item, itemType)), ("status", status)));

        if (!completed || itemType != "fileChange" ||
            !TryProperty(item, "changes", out var changes) ||
            changes.ValueKind is not JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var change in changes.EnumerateArray())
        {
            var path = OptionalString(change, "path");
            if (path is null)
                continue;
            Add(
                events,
                StableEventId("file", itemId, index++.ToString(CultureInfo.InvariantCulture), path),
                WorkflowJournalEventKind.FileWritten,
                timestamp,
                threadId,
                turnId,
                attemptId,
                AgentId(threadId),
                ParentAgentId(threadId),
                null,
                itemId,
                contentRefs,
                Data(("path", path), ("status", status)));
        }
    }

    private void NormalizeThreadStatus(
        JsonElement parameters,
        DateTimeOffset timestamp,
        List<WorkflowEventAppend> events)
    {
        if (!TryString(parameters, "threadId", out var threadId) ||
            !IsObservedThread(threadId) ||
            threadId == _rootThreadId ||
            !TryProperty(parameters, "status", out var status) ||
            !TryString(status, "type", out var type) ||
            type is not "systemError")
        {
            return;
        }

        Add(
            events,
            StableEventId("agent", threadId, "system-error"),
            WorkflowJournalEventKind.AgentCompleted,
            timestamp,
            threadId,
            _threads[threadId].ActiveTurnId,
            CurrentAttemptId(threadId),
            threadId,
            ParentAgentId(threadId),
            null,
            null,
            [],
            Data(("status", "failed")));
    }

    private void NormalizeApprovalRequested(
        JsonElement message,
        string method,
        JsonElement parameters,
        DateTimeOffset receivedAt,
        List<WorkflowEventAppend> events,
        Dictionary<string, WorkflowContentChunk> content)
    {
        if (!TryString(parameters, "threadId", out var threadId) ||
            !TryString(parameters, "turnId", out var turnId) ||
            !IsObservedThread(threadId))
        {
            return;
        }
        var requestId = message.TryGetProperty("id", out var id)
            ? id.ToString()
            : StableEventId(method, threadId, turnId);
        var itemId = OptionalString(parameters, "itemId");
        var chunk = Capture(parameters);
        content.TryAdd(chunk.ContentRef, chunk);
        _approvals[requestId] = new ApprovalContext(threadId, turnId, itemId, CurrentAttemptId(threadId));
        Add(
            events,
            StableEventId("approval", requestId, "requested"),
            WorkflowJournalEventKind.ApprovalRequested,
            UnixMilliseconds(parameters, "startedAtMs") ?? receivedAt,
            threadId,
            turnId,
            CurrentAttemptId(threadId),
            AgentId(threadId),
            ParentAgentId(threadId),
            null,
            itemId,
            [chunk.ContentRef],
            Data(("approval_id", requestId), ("label", method)));
    }

    private void NormalizeApprovalResolved(
        JsonElement parameters,
        DateTimeOffset timestamp,
        List<WorkflowEventAppend> events)
    {
        if (!TryString(parameters, "threadId", out var threadId) ||
            !parameters.TryGetProperty("requestId", out var requestIdElement))
        {
            return;
        }
        var requestId = requestIdElement.ToString();
        if (!_approvals.Remove(requestId, out var approval))
            return;
        Add(
            events,
            StableEventId("approval", requestId, "resolved"),
            WorkflowJournalEventKind.ApprovalResolved,
            timestamp,
            threadId,
            approval.TurnId,
            approval.AttemptId,
            AgentId(threadId),
            ParentAgentId(threadId),
            null,
            approval.ItemId,
            [],
            Data(("approval_id", requestId), ("status", "completed")));
    }

    private void NormalizeAutoApproval(
        JsonElement parameters,
        DateTimeOffset timestamp,
        bool completed,
        List<WorkflowEventAppend> events,
        Dictionary<string, WorkflowContentChunk> content)
    {
        if (!TryString(parameters, "threadId", out var threadId) ||
            !TryString(parameters, "turnId", out var turnId) ||
            !IsObservedThread(threadId))
        {
            return;
        }
        var itemId = OptionalString(parameters, "itemId") ?? "unknown";
        var chunk = Capture(parameters);
        content.TryAdd(chunk.ContentRef, chunk);
        Add(
            events,
            StableEventId("auto-approval", threadId, turnId, itemId, completed ? "completed" : "started"),
            completed ? WorkflowJournalEventKind.ApprovalResolved : WorkflowJournalEventKind.ApprovalRequested,
            timestamp,
            threadId,
            turnId,
            CurrentAttemptId(threadId),
            AgentId(threadId),
            ParentAgentId(threadId),
            null,
            itemId,
            [chunk.ContentRef],
            Data(("approval_id", itemId), ("status", completed ? "completed" : "pending")));
    }

    private void Add(
        List<WorkflowEventAppend> events,
        string eventId,
        WorkflowJournalEventKind kind,
        DateTimeOffset timestamp,
        string? threadId,
        string? turnId,
        string? attemptId,
        string? agentId,
        string? parentAgentId,
        string? receiverAgentId,
        string? toolCallId,
        IReadOnlyList<string> contentRefs,
        IReadOnlyDictionary<string, object>? data)
    {
        var workflowEvent = CreateEvent(
            eventId,
            kind,
            timestamp,
            threadId,
            turnId,
            attemptId,
            agentId,
            parentAgentId,
            receiverAgentId,
            toolCallId,
            contentRefs,
            data);
        if (workflowEvent is not null)
            events.Add(workflowEvent);
    }

    private WorkflowEventAppend? CreateEvent(
        string eventId,
        WorkflowJournalEventKind kind,
        DateTimeOffset timestamp,
        string? threadId,
        string? turnId,
        string? attemptId,
        string? agentId,
        string? parentAgentId,
        string? receiverAgentId,
        string? toolCallId,
        IReadOnlyList<string> contentRefs,
        IReadOnlyDictionary<string, object>? data)
    {
        if (!_eventIds.Add(eventId))
            return null;
        return new WorkflowEventAppend
        {
            EventId = eventId,
            SourceSequence = ++_sourceSequence,
            Timestamp = timestamp,
            Kind = kind,
            ThreadId = threadId,
            TurnId = turnId,
            AttemptId = attemptId,
            AgentId = agentId,
            ParentAgentId = parentAgentId,
            ReceiverAgentId = receiverAgentId,
            ToolCallId = toolCallId,
            ContentRefs = contentRefs.Count is 0 ? null : contentRefs,
            Data = data
        };
    }

    private bool IsObservedThread(string threadId)
    {
        if (threadId == _rootThreadId)
            return true;
        for (var cursor = threadId; _threads.TryGetValue(cursor, out var context);)
        {
            if (context.ParentThreadId == _rootThreadId)
                return true;
            if (context.ParentThreadId is null)
                return false;
            cursor = context.ParentThreadId;
        }
        return false;
    }

    private string? CurrentAttemptId(string threadId)
    {
        if (_threads.TryGetValue(threadId, out var context) && context.AttemptId is not null)
            return context.AttemptId;
        return _rootThreadId is not null && _threads.TryGetValue(_rootThreadId, out var root)
            ? root.AttemptId
            : null;
    }

    private string? AgentId(string threadId) =>
        threadId == _rootThreadId ? null : threadId;

    private string? ParentAgentId(string threadId)
    {
        if (!_threads.TryGetValue(threadId, out var context) ||
            context.ParentThreadId is null ||
            context.ParentThreadId == _rootThreadId)
        {
            return null;
        }
        return context.ParentThreadId;
    }

    private static WorkflowContentChunk Capture(JsonElement value)
    {
        var json = value.GetRawText();
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        return new WorkflowContentChunk
        {
            ContentRef = $"sha256:{hash}",
            ContentType = "application/json",
            Encoding = WorkflowContentEncoding.Utf8,
            Content = json
        };
    }

    private static IReadOnlyDictionary<string, object>? Data(
        params (string Name, string? Value)[] values)
    {
        Dictionary<string, object>? result = null;
        foreach (var (name, value) in values)
        {
            if (value is null)
                continue;
            result ??= new Dictionary<string, object>(StringComparer.Ordinal);
            result.Add(name, value);
        }
        return result;
    }

    private static string ItemLabel(JsonElement item)
    {
        var type = OptionalString(item, "type") ?? "item";
        return type switch
        {
            "commandExecution" => OptionalString(item, "command") ?? "Command",
            "fileChange" => "File change",
            "mcpToolCall" => ToolLabel(item, type),
            "dynamicToolCall" => ToolLabel(item, type),
            "collabAgentToolCall" => OptionalString(item, "tool") ?? "Collaboration",
            "agentMessage" => "Agent result",
            "userMessage" => "Prompt",
            _ => type
        };
    }

    private static string ToolLabel(JsonElement item, string itemType) =>
        itemType switch
        {
            "commandExecution" => "shell",
            "fileChange" => "apply_patch",
            "mcpToolCall" =>
                $"{OptionalString(item, "server") ?? "mcp"}/{OptionalString(item, "tool") ?? "tool"}",
            "dynamicToolCall" =>
                $"{OptionalString(item, "namespace") ?? "dynamic"}/{OptionalString(item, "tool") ?? "tool"}",
            "webSearch" => "web_search",
            "imageView" => "image_view",
            "imageGeneration" => "image_generation",
            "sleep" => "sleep",
            _ => itemType
        };

    private static string AgentLabel(JsonElement item, string fallback) =>
        OptionalString(item, "model") is { } model
            ? $"{fallback} · {model}"
            : fallback;

    private static string ItemStatus(JsonElement item)
    {
        var status = OptionalString(item, "status");
        if (status is not null)
            return NormalizeStatus(status);
        if (item.TryGetProperty("success", out var success) &&
            success.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return success.GetBoolean() ? "succeeded" : "failed";
        }
        return "completed";
    }

    private static string NormalizeStatus(string status) => status switch
    {
        "completed" or "succeeded" => "succeeded",
        "inProgress" or "running" or "active" => "running",
        "declined" or "rejected" => "rejected",
        "interrupted" => "interrupted",
        "failed" or "systemError" => "failed",
        _ => status
    };

    private static string StableEventId(params string[] parts)
    {
        var candidate = string.Join(':', parts);
        if (candidate.Length <= 150)
            return candidate;
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(candidate)));
        return $"codex:{hash}";
    }

    private static bool TryProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind is JsonValueKind.Object && element.TryGetProperty(name, out value))
            return true;
        value = default;
        return false;
    }

    private static bool TryString(JsonElement element, string name, out string value)
    {
        value = "";
        return TryProperty(element, name, out var property) &&
               property.ValueKind is JsonValueKind.String &&
               (value = property.GetString()!) is not null;
    }

    private static string RequiredString(JsonElement element, string name) =>
        TryString(element, name, out var value)
            ? value
            : throw new InvalidDataException($"Codex app-server item omitted required string '{name}'.");

    private static string? OptionalString(JsonElement element, string name) =>
        TryString(element, name, out var value) ? value : null;

    private static IReadOnlyList<string> StringArray(JsonElement element, string name)
    {
        if (!TryProperty(element, name, out var property) ||
            property.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }
        return property.EnumerateArray()
            .Where(static item => item.ValueKind is JsonValueKind.String)
            .Select(static item => item.GetString()!)
            .ToArray();
    }

    private static DateTimeOffset? UnixSeconds(JsonElement element, string name) =>
        TryProperty(element, name, out var value) &&
        value.ValueKind is JsonValueKind.Number &&
        value.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    private static DateTimeOffset? UnixMilliseconds(JsonElement element, string name) =>
        TryProperty(element, name, out var value) &&
        value.ValueKind is JsonValueKind.Number &&
        value.TryGetInt64(out var milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
            : null;

    private sealed record ThreadContext(
        string? ParentThreadId,
        string? AttemptId,
        string? ActiveTurnId);

    private sealed record ApprovalContext(
        string ThreadId,
        string TurnId,
        string? ItemId,
        string? AttemptId);
}
