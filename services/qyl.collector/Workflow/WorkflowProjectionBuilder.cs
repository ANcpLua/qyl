using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Workflow;

internal static class WorkflowProjectionBuilder
{
    private const int MaxRecordedEventIds = 32;

    private const int MaxConflictWitnessesPerPath = 32;

    private const int MaxNodeIdLength = 192;

    public static WorkflowGraphSnapshot Build(
        WorkflowRunStorageRow run,
        IReadOnlyList<WorkflowEventStorageRow> events,
        DateTimeOffset now)
    {
        var nodes = new Dictionary<string, MutableNode>(StringComparer.Ordinal);
        var edges = new Dictionary<string, WorkflowGraphEdge>(StringComparer.Ordinal);
        // Derived from the journal alone. Seeding this from run.ActiveAttemptId — a column the
        // append path rewrites — made the projection depend on state outside the journal it
        // claims to be a deterministic function of. Build always receives the complete event
        // list, so every attempt the journal established is rediscovered from AttemptStarted.
        string? activeAttempt = null;
        var lastNodeByOwner = new Dictionary<string, string>(StringComparer.Ordinal);
        var writesByPath = new Dictionary<string, List<(string NodeId, string EventId)>>(StringComparer.Ordinal);

        var runNodeId = NodeId("run", run.RunId);
        nodes.Add(runNodeId, new MutableNode(
            runNodeId,
            WorkflowNodeKind.Run,
            run.Title ?? run.RunId,
            RunStatus(run.Status),
            null,
            null,
            null,
            run.StartedAt,
            run.EndedAt,
            []));

        foreach (var workflowEvent in events.OrderBy(static item => item.JournalSequence))
        {
            if (workflowEvent.Kind is WorkflowJournalEventKind.AttemptStarted)
                activeAttempt = workflowEvent.AttemptId ?? activeAttempt;

            var attemptId = workflowEvent.AttemptId ?? activeAttempt;
            var attemptNodeId = attemptId is null ? null : NodeId("attempt", attemptId);
            if (attemptNodeId is not null && !nodes.ContainsKey(attemptNodeId))
            {
                nodes.Add(attemptNodeId, new MutableNode(
                    attemptNodeId,
                    WorkflowNodeKind.Attempt,
                    $"Attempt {attemptId}",
                    "running",
                    attemptId,
                    null,
                    runNodeId,
                    workflowEvent.Timestamp,
                    null,
                    []));
                AddRecordedEdge(edges, runNodeId, attemptNodeId, WorkflowEdgeKind.Temporal, workflowEvent.EventId);
            }

            switch (workflowEvent.Kind)
            {
                case WorkflowJournalEventKind.AttemptStarted:
                    if (attemptNodeId is not null)
                        UpdateLifecycle(nodes[attemptNodeId], workflowEvent, "running", starts: true);
                    break;

                case WorkflowJournalEventKind.AttemptCompleted:
                    if (attemptNodeId is not null)
                    {
                        UpdateLifecycle(
                            nodes[attemptNodeId],
                            workflowEvent,
                            EventStatus(workflowEvent, "succeeded"),
                            ends: true);
                    }
                    break;

                case WorkflowJournalEventKind.TurnStarted:
                case WorkflowJournalEventKind.TurnCompleted:
                case WorkflowJournalEventKind.TurnInterrupted:
                    ProjectTurn(nodes, edges, workflowEvent, attemptId, attemptNodeId);
                    break;

                case WorkflowJournalEventKind.AgentSpawned:
                case WorkflowJournalEventKind.AgentStarted:
                case WorkflowJournalEventKind.AgentCompleted:
                    ProjectAgent(nodes, edges, workflowEvent, attemptId, attemptNodeId);
                    break;

                case WorkflowJournalEventKind.ToolStarted:
                case WorkflowJournalEventKind.ToolCompleted:
                    ProjectTool(nodes, edges, workflowEvent, attemptId, attemptNodeId);
                    break;

                case WorkflowJournalEventKind.MessageSent:
                case WorkflowJournalEventKind.MessageReceived:
                    ProjectMessage(nodes, edges, workflowEvent, attemptId, attemptNodeId);
                    break;

                case WorkflowJournalEventKind.WaitStarted:
                case WorkflowJournalEventKind.WaitCompleted:
                case WorkflowJournalEventKind.Joined:
                    ProjectWait(nodes, edges, workflowEvent, attemptId, attemptNodeId);
                    break;

                case WorkflowJournalEventKind.ApprovalRequested:
                case WorkflowJournalEventKind.ApprovalResolved:
                case WorkflowJournalEventKind.ControlRequested:
                case WorkflowJournalEventKind.ControlAccepted:
                case WorkflowJournalEventKind.ControlApplied:
                case WorkflowJournalEventKind.ControlRejected:
                case WorkflowJournalEventKind.ControlFailed:
                    ProjectGate(nodes, edges, workflowEvent, attemptId, attemptNodeId);
                    break;

                case WorkflowJournalEventKind.FileWritten:
                    ProjectFile(nodes, edges, writesByPath, workflowEvent, attemptId, attemptNodeId);
                    break;

                case WorkflowJournalEventKind.ItemStarted:
                case WorkflowJournalEventKind.ItemCompleted:
                    ProjectItem(nodes, edges, workflowEvent, attemptId, attemptNodeId);
                    break;
            }

            var owner = OwnerNodeId(workflowEvent, attemptId, attemptNodeId);
            var eventNode = NodeForEvent(workflowEvent, attemptId);
            if (eventNode is not null && owner is not null)
            {
                if (lastNodeByOwner.TryGetValue(owner, out var previous) && previous != eventNode)
                    AddRecordedEdge(edges, previous, eventNode, WorkflowEdgeKind.Temporal, workflowEvent.EventId);
                lastNodeByOwner[owner] = eventNode;
            }
        }

        var projectedNodes = nodes.Values
            .Select(node => node.ToContract(now))
            .OrderBy(static node => node.StartedAt)
            .ThenBy(static node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
        var projectedEdges = edges.Values
            .OrderBy(static edge => edge.EdgeId, StringComparer.Ordinal)
            .ToArray();
        var statistics = CalculateStatistics(projectedNodes, projectedEdges, run, now);

        return new WorkflowGraphSnapshot
        {
            Run = ToContract(run),
            Nodes = projectedNodes,
            Edges = projectedEdges,
            Statistics = statistics,
            JournalSequence = run.LatestJournalSequence,
            NextNodeCursor = null,
            NextEdgeCursor = null,
            HasMoreNodes = false,
            HasMoreEdges = false,
            TotalNodeCount = projectedNodes.Length,
            TotalEdgeCount = projectedEdges.Length
        };
    }

    internal static WorkflowRun ToContract(WorkflowRunStorageRow run) =>
        new()
        {
            RunId = run.RunId,
            ThreadId = run.ThreadId,
            Title = run.Title,
            Status = run.Status,
            StartedAt = run.StartedAt,
            EndedAt = run.EndedAt,
            LatestJournalSequence = run.LatestJournalSequence,
            ActiveAttemptId = run.ActiveAttemptId,
            Metadata = ParseObject(run.MetadataJson)
        };

    private static void ProjectTurn(
        Dictionary<string, MutableNode> nodes,
        Dictionary<string, WorkflowGraphEdge> edges,
        WorkflowEventStorageRow workflowEvent,
        string? attemptId,
        string? attemptNodeId)
    {
        if (workflowEvent.TurnId is null)
            return;

        var nodeId = TurnNodeId(attemptId, workflowEvent.TurnId);
        if (!nodes.TryGetValue(nodeId, out var node))
        {
            node = new MutableNode(
                nodeId,
                WorkflowNodeKind.Turn,
                EventLabel(workflowEvent, workflowEvent.TurnId),
                "running",
                attemptId,
                null,
                attemptNodeId,
                workflowEvent.Timestamp,
                null,
                workflowEvent.ContentRefs);
            nodes.Add(nodeId, node);
        }

        if (workflowEvent.Kind is WorkflowJournalEventKind.TurnStarted)
            UpdateLifecycle(node, workflowEvent, "running", starts: true);
        else
            UpdateLifecycle(
                node,
                workflowEvent,
                workflowEvent.Kind is WorkflowJournalEventKind.TurnInterrupted
                    ? "interrupted"
                    : EventStatus(workflowEvent, "succeeded"),
                ends: true);

        if (attemptNodeId is not null)
            AddRecordedEdge(edges, attemptNodeId, nodeId, WorkflowEdgeKind.Control, workflowEvent.EventId);
    }

    private static void ProjectAgent(
        Dictionary<string, MutableNode> nodes,
        Dictionary<string, WorkflowGraphEdge> edges,
        WorkflowEventStorageRow workflowEvent,
        string? attemptId,
        string? attemptNodeId)
    {
        if (workflowEvent.AgentId is null)
            return;

        var nodeId = AgentNodeId(attemptId, workflowEvent.AgentId);
        var parentNodeId = workflowEvent.ParentAgentId is null
            ? attemptNodeId
            : AgentNodeId(attemptId, workflowEvent.ParentAgentId);
        if (!nodes.TryGetValue(nodeId, out var node))
        {
            node = new MutableNode(
                nodeId,
                WorkflowNodeKind.Agent,
                EventLabel(workflowEvent, workflowEvent.AgentId),
                workflowEvent.Kind is WorkflowJournalEventKind.AgentSpawned ? "pending" : "running",
                attemptId,
                workflowEvent.AgentId,
                parentNodeId,
                workflowEvent.Timestamp,
                null,
                workflowEvent.ContentRefs);
            nodes.Add(nodeId, node);
        }

        if (workflowEvent.Kind is WorkflowJournalEventKind.AgentStarted)
            UpdateLifecycle(node, workflowEvent, "running", starts: true);
        else if (workflowEvent.Kind is WorkflowJournalEventKind.AgentCompleted)
            UpdateLifecycle(node, workflowEvent, EventStatus(workflowEvent, "succeeded"), ends: true);
        else
            node.AddContent(workflowEvent.ContentRefs);

        if (parentNodeId is not null)
            AddRecordedEdge(edges, parentNodeId, nodeId, WorkflowEdgeKind.Control, workflowEvent.EventId);
    }

    private static void ProjectTool(
        Dictionary<string, MutableNode> nodes,
        Dictionary<string, WorkflowGraphEdge> edges,
        WorkflowEventStorageRow workflowEvent,
        string? attemptId,
        string? attemptNodeId)
    {
        if (workflowEvent.ToolCallId is null)
            return;

        var nodeId = ToolNodeId(attemptId, workflowEvent.ToolCallId);
        var parentNodeId = workflowEvent.AgentId is null
            ? attemptNodeId
            : AgentNodeId(attemptId, workflowEvent.AgentId);
        if (!nodes.TryGetValue(nodeId, out var node))
        {
            node = new MutableNode(
                nodeId,
                WorkflowNodeKind.ToolCall,
                EventLabel(workflowEvent, workflowEvent.ToolCallId),
                "running",
                attemptId,
                workflowEvent.AgentId,
                parentNodeId,
                workflowEvent.Timestamp,
                null,
                workflowEvent.ContentRefs);
            nodes.Add(nodeId, node);
        }

        if (workflowEvent.Kind is WorkflowJournalEventKind.ToolCompleted)
            UpdateLifecycle(node, workflowEvent, EventStatus(workflowEvent, "succeeded"), ends: true);
        else
            UpdateLifecycle(node, workflowEvent, "running", starts: true);

        if (parentNodeId is not null)
            AddRecordedEdge(edges, parentNodeId, nodeId, WorkflowEdgeKind.Control, workflowEvent.EventId);
    }

    private static void ProjectMessage(
        Dictionary<string, MutableNode> nodes,
        Dictionary<string, WorkflowGraphEdge> edges,
        WorkflowEventStorageRow workflowEvent,
        string? attemptId,
        string? attemptNodeId)
    {
        var nodeId = NodeId("message", workflowEvent.EventId);
        var sender = workflowEvent.AgentId is null
            ? attemptNodeId
            : AgentNodeId(attemptId, workflowEvent.AgentId);
        var receiver = workflowEvent.ReceiverAgentId is null
            ? null
            : AgentNodeId(attemptId, workflowEvent.ReceiverAgentId);
        nodes[nodeId] = new MutableNode(
            nodeId,
            WorkflowNodeKind.Message,
            EventLabel(workflowEvent, "Message"),
            "completed",
            attemptId,
            workflowEvent.AgentId,
            sender,
            workflowEvent.Timestamp,
            workflowEvent.Timestamp,
            workflowEvent.ContentRefs);
        if (sender is not null)
            AddRecordedEdge(edges, sender, nodeId, WorkflowEdgeKind.Data, workflowEvent.EventId);
        if (receiver is not null)
            AddRecordedEdge(edges, nodeId, receiver, WorkflowEdgeKind.Data, workflowEvent.EventId);
    }

    private static void ProjectWait(
        Dictionary<string, MutableNode> nodes,
        Dictionary<string, WorkflowGraphEdge> edges,
        WorkflowEventStorageRow workflowEvent,
        string? attemptId,
        string? attemptNodeId)
    {
        var owner = workflowEvent.AgentId is null
            ? attemptNodeId
            : AgentNodeId(attemptId, workflowEvent.AgentId);
        var stableId = DataString(workflowEvent, "wait_id") ?? workflowEvent.ToolCallId ?? workflowEvent.EventId;
        var nodeId = NodeId("wait", attemptId ?? "run", stableId);
        if (!nodes.TryGetValue(nodeId, out var node))
        {
            node = new MutableNode(
                nodeId,
                WorkflowNodeKind.Wait,
                EventLabel(workflowEvent, "Wait"),
                "waiting",
                attemptId,
                workflowEvent.AgentId,
                owner,
                workflowEvent.Timestamp,
                null,
                workflowEvent.ContentRefs);
            nodes.Add(nodeId, node);
        }
        if (workflowEvent.Kind is WorkflowJournalEventKind.WaitCompleted or WorkflowJournalEventKind.Joined)
            UpdateLifecycle(node, workflowEvent, "completed", ends: true);
        if (owner is not null)
            AddRecordedEdge(edges, owner, nodeId, WorkflowEdgeKind.Temporal, workflowEvent.EventId);
        if (workflowEvent.ReceiverAgentId is { } joinedAgent)
            AddRecordedEdge(
                edges,
                AgentNodeId(attemptId, joinedAgent),
                nodeId,
                WorkflowEdgeKind.Gate,
                workflowEvent.EventId);
    }

    private static void ProjectGate(
        Dictionary<string, MutableNode> nodes,
        Dictionary<string, WorkflowGraphEdge> edges,
        WorkflowEventStorageRow workflowEvent,
        string? attemptId,
        string? attemptNodeId)
    {
        var owner = OwnerNodeId(workflowEvent, attemptId, attemptNodeId);
        var stableId = DataString(workflowEvent, "command_id") ??
                       DataString(workflowEvent, "approval_id") ??
                       workflowEvent.EventId;
        var nodeId = NodeId("gate", stableId);
        var terminal = workflowEvent.Kind is
            WorkflowJournalEventKind.ApprovalResolved or
            WorkflowJournalEventKind.ControlApplied or
            WorkflowJournalEventKind.ControlRejected or
            WorkflowJournalEventKind.ControlFailed;
        if (!nodes.TryGetValue(nodeId, out var node))
        {
            node = new MutableNode(
                nodeId,
                WorkflowNodeKind.Gate,
                EventLabel(workflowEvent, "Control gate"),
                terminal ? EventStatus(workflowEvent, "completed") : "pending",
                attemptId,
                workflowEvent.AgentId,
                owner,
                workflowEvent.Timestamp,
                terminal ? workflowEvent.Timestamp : null,
                workflowEvent.ContentRefs);
            nodes.Add(nodeId, node);
        }
        else if (terminal)
        {
            UpdateLifecycle(node, workflowEvent, EventStatus(workflowEvent, "completed"), ends: true);
        }
        if (owner is not null)
            AddRecordedEdge(edges, owner, nodeId, WorkflowEdgeKind.Gate, workflowEvent.EventId);
    }

    private static void ProjectFile(
        Dictionary<string, MutableNode> nodes,
        Dictionary<string, WorkflowGraphEdge> edges,
        Dictionary<string, List<(string NodeId, string EventId)>> writesByPath,
        WorkflowEventStorageRow workflowEvent,
        string? attemptId,
        string? attemptNodeId)
    {
        var path = DataString(workflowEvent, "path");
        var owner = OwnerNodeId(workflowEvent, attemptId, attemptNodeId);
        if (path is null || owner is null)
            return;

        var resourceId = $"resource:file:{ShortHash(path)}";
        if (!nodes.ContainsKey(resourceId))
        {
            nodes.Add(resourceId, new MutableNode(
                resourceId,
                WorkflowNodeKind.Resource,
                path,
                "written",
                null,
                null,
                null,
                workflowEvent.Timestamp,
                workflowEvent.Timestamp,
                workflowEvent.ContentRefs));
        }
        AddRecordedEdge(edges, owner, resourceId, WorkflowEdgeKind.Resource, workflowEvent.EventId);

        var attemptPath = $"{attemptId ?? "run"}\0{path}";
        if (!writesByPath.TryGetValue(attemptPath, out var previousWrites))
        {
            previousWrites = [];
            writesByPath.Add(attemptPath, previousWrites);
        }
        foreach (var previous in previousWrites.Where(previous => previous.NodeId != owner))
        {
            var source = string.CompareOrdinal(previous.NodeId, owner) <= 0 ? previous.NodeId : owner;
            var target = source == owner ? previous.NodeId : owner;
            var edgeId = $"conflict:{ShortHash(path)}:{source}:{target}";
            edges[edgeId] = new WorkflowGraphEdge
            {
                EdgeId = edgeId,
                SourceNodeId = source,
                TargetNodeId = target,
                Kind = WorkflowEdgeKind.Conflict,
                Provenance = new DerivedWorkflowEdgeProvenance
                {
                    EventIds = [previous.EventId, workflowEvent.EventId],
                    Evidence = $"Both agents wrote {path}",
                    Confidence = 0.85
                }
            };
        }
        if (previousWrites.Count < MaxConflictWitnessesPerPath)
            previousWrites.Add((owner, workflowEvent.EventId));
    }

    private static void ProjectItem(
        Dictionary<string, MutableNode> nodes,
        Dictionary<string, WorkflowGraphEdge> edges,
        WorkflowEventStorageRow workflowEvent,
        string? attemptId,
        string? attemptNodeId)
    {
        var nodeId = NodeId("item", DataString(workflowEvent, "item_id") ?? workflowEvent.EventId);
        var owner = OwnerNodeId(workflowEvent, attemptId, attemptNodeId);
        if (!nodes.TryGetValue(nodeId, out var node))
        {
            node = new MutableNode(
                nodeId,
                WorkflowNodeKind.Message,
                EventLabel(workflowEvent, "Item"),
                "running",
                attemptId,
                workflowEvent.AgentId,
                owner,
                workflowEvent.Timestamp,
                null,
                workflowEvent.ContentRefs);
            nodes.Add(nodeId, node);
        }
        if (workflowEvent.Kind is WorkflowJournalEventKind.ItemCompleted)
            UpdateLifecycle(node, workflowEvent, EventStatus(workflowEvent, "completed"), ends: true);
        if (owner is not null)
            AddRecordedEdge(edges, owner, nodeId, WorkflowEdgeKind.Data, workflowEvent.EventId);
    }

    private static WorkflowGraphStatistics CalculateStatistics(
        IReadOnlyList<WorkflowGraphNode> nodes,
        IReadOnlyList<WorkflowGraphEdge> edges,
        WorkflowRunStorageRow run,
        DateTimeOffset now)
    {
        var timed = nodes.Where(static node =>
                node.Kind is WorkflowNodeKind.Agent or WorkflowNodeKind.ToolCall or WorkflowNodeKind.Wait)
            .Where(static node => node.StartedAt.HasValue)
            .ToArray();
        var nodeById = nodes.ToDictionary(static node => node.NodeId, StringComparer.Ordinal);
        var agentIntervals = timed
            .Where(static node => node.Kind is WorkflowNodeKind.Agent)
            .Select(node => Interval(node.StartedAt!.Value, node.EndedAt ?? now))
            .ToArray();
        var peakConcurrency = PeakConcurrency(agentIntervals);
        var workerCount = Math.Max(
            Math.Max(1, peakConcurrency),
            timed
                .Where(static node => node.Kind is WorkflowNodeKind.Agent && node.AgentId is not null)
                .Select(static node => node.AgentId)
                .Distinct(StringComparer.Ordinal)
                .Count());

        var childIntervals = edges
            .Where(static edge => edge.Kind is WorkflowEdgeKind.Control)
            .GroupBy(static edge => edge.SourceNodeId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                group => group
                    .Select(edge => nodeById.GetValueOrDefault(edge.TargetNodeId))
                    .Where(static node => node?.StartedAt is not null)
                    .Select(node => Interval(node!.StartedAt!.Value, node.EndedAt ?? now))
                    .ToArray(),
                StringComparer.Ordinal);

        var weights = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var duration = node.DurationMs ?? 0;
            if (node.Kind is WorkflowNodeKind.Agent &&
                childIntervals.TryGetValue(node.NodeId, out var children))
            {
                duration = Math.Max(0, duration - UnionDurationMs(children));
            }
            else if (node.Kind is WorkflowNodeKind.Run or WorkflowNodeKind.Attempt
                     or WorkflowNodeKind.Turn or WorkflowNodeKind.Resource)
            {
                // Container nodes span the work nested inside them. Charging their own
                // elapsed time counts that work twice in T1.
                duration = 0;
            }
            weights[node.NodeId] = duration;
        }

        // T1 and T-infinity MUST be summed over the same weight function. Excluding Wait
        // from T1 while LongestPath still traversed its weight let tInfinityMs exceed t1Ms,
        // which is impossible in work-span analysis (the span is a path through the work)
        // and made parallelLowerBoundMs claim a floor above the fully serial time.
        var t1 = weights.Sum(static pair => pair.Value);
        var (tInfinity, criticalPath) = LongestPath(nodes, edges, weights);
        var wallTime = Math.Max(0, ((run.EndedAt ?? now) - run.StartedAt).TotalMilliseconds);
        return new WorkflowGraphStatistics
        {
            T1Ms = t1,
            TInfinityMs = tInfinity,
            WallTimeMs = wallTime,
            PeakConcurrency = peakConcurrency,
            WorkerCount = workerCount,
            ParallelLowerBoundMs = Math.Max(t1 / workerCount, tInfinity),
            CriticalPathNodeIds = criticalPath
        };
    }

    private static (double Duration, IReadOnlyList<string> Path) LongestPath(
        IReadOnlyList<WorkflowGraphNode> nodes,
        IReadOnlyList<WorkflowGraphEdge> edges,
        IReadOnlyDictionary<string, double> weights)
    {
        // Tie-break order: deterministic, and the order cycle-broken nodes fall back to.
        var ordered = nodes
            .OrderBy(static node => node.StartedAt ?? DateTimeOffset.MinValue)
            .ThenBy(static node => node.NodeId, StringComparer.Ordinal)
            .Select(static node => node.NodeId)
            .ToArray();
        var rank = ordered
            .Select(static (id, position) => (id, position))
            .ToDictionary(static pair => pair.id, static pair => pair.position, StringComparer.Ordinal);

        // The span may only traverse REAL dependencies. A Temporal edge records that two nodes
        // shared an owner and one followed the other (the lastNodeByOwner chain) — correlation,
        // not causation — and counting it inflates tInfinityMs with serialization that infinite
        // workers would remove. A Data edge is the only representation of cross-agent causality,
        // so excluding it deflated the span at the same time. Resource and Conflict edges are
        // contention, not dependency.
        var dependencies = edges
            .Where(static edge => edge.Kind is
                WorkflowEdgeKind.Data or WorkflowEdgeKind.Control or WorkflowEdgeKind.Gate)
            .Where(edge => rank.ContainsKey(edge.SourceNodeId) && rank.ContainsKey(edge.TargetNodeId))
            .Where(static edge => !string.Equals(edge.SourceNodeId, edge.TargetNodeId, StringComparison.Ordinal))
            .Select(static edge => (Source: edge.SourceNodeId, Target: edge.TargetNodeId))
            .Distinct()
            .ToArray();

        // Acyclicity used to be approximated by requiring the source to start before the target.
        // That is not what a dependency means: a long-lived agent can receive a message it
        // depends on well after it started, and every such edge was silently deleted. Sort for
        // real (Kahn) so only edges that genuinely close a cycle are dropped.
        var successors = dependencies
            .GroupBy(static edge => edge.Source, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static edge => edge.Target).ToArray(),
                StringComparer.Ordinal);
        var predecessors = dependencies
            .GroupBy(static edge => edge.Target, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                group => group.Select(static edge => edge.Source).OrderBy(id => rank[id]).ToArray(),
                StringComparer.Ordinal);

        var inDegree = ordered.ToDictionary(static id => id, static _ => 0, StringComparer.Ordinal);
        foreach (var (_, target) in dependencies)
            inDegree[target]++;

        // Ready set keyed by rank: identical input always yields an identical topological order.
        var ready = new PriorityQueue<string, int>();
        foreach (var id in ordered)
        {
            if (inDegree[id] is 0)
                ready.Enqueue(id, rank[id]);
        }

        var topological = new List<string>(ordered.Length);
        var settled = new HashSet<string>(StringComparer.Ordinal);
        while (ready.TryDequeue(out var id, out _))
        {
            topological.Add(id);
            settled.Add(id);
            if (!successors.TryGetValue(id, out var next))
                continue;
            foreach (var target in next)
            {
                if (--inDegree[target] is 0)
                    ready.Enqueue(target, rank[target]);
            }
        }

        // Anything left sits on a cycle (an agent can message an agent that messaged it).
        // Append it in rank order and relax it against already-settled predecessors only,
        // which keeps the pass finite and the result deterministic.
        foreach (var id in ordered)
        {
            if (settled.Add(id))
                topological.Add(id);
        }

        var scores = new Dictionary<string, double>(StringComparer.Ordinal);
        var previous = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var id in topological)
        {
            var bestScore = 0d;
            string? bestPrevious = null;
            if (predecessors.TryGetValue(id, out var candidates))
            {
                foreach (var candidate in candidates)
                {
                    if (!scores.TryGetValue(candidate, out var candidateScore))
                        continue;
                    // Track the best predecessor even when it scores zero; anchoring on 0
                    // truncated the critical path at every zero-weight container node.
                    if (bestPrevious is null || candidateScore > bestScore)
                    {
                        bestScore = candidateScore;
                        bestPrevious = candidate;
                    }
                }
            }
            scores[id] = bestScore + weights.GetValueOrDefault(id);
            previous[id] = bestPrevious;
        }

        if (scores.Count is 0)
            return (0, []);
        // The snapshot contract promises a deterministic projection: break score ties on
        // the node id rather than on dictionary enumeration order.
        var end = scores
            .OrderByDescending(static pair => pair.Value)
            .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
            .First().Key;
        var path = new List<string>();
        for (string? cursor = end; cursor is not null; cursor = previous[cursor])
            path.Add(cursor);
        path.Reverse();
        return (scores[end], path);
    }

    private static int PeakConcurrency(IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> intervals)
    {
        var points = intervals
            .SelectMany(static interval => new[] { (interval.Start, 1), (interval.End, -1) })
            .OrderBy(static point => point.Item1)
            .ThenBy(static point => point.Item2)
            .ToArray();
        var active = 0;
        var peak = 0;
        foreach (var (_, delta) in points)
        {
            active += delta;
            peak = Math.Max(peak, active);
        }
        return peak;
    }

    /// <summary>
    /// Each agent stamps its own clock, so an untrusted or skewed journal can report an end
    /// before its start. Clamping at the point of construction keeps the concurrency sweep
    /// and the union-duration merge from going negative on hostile or merely unlucky input.
    /// </summary>
    private static (DateTimeOffset Start, DateTimeOffset End) Interval(
        DateTimeOffset start,
        DateTimeOffset end) =>
        (start, end < start ? start : end);

    private static double UnionDurationMs(
        IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> intervals)
    {
        var ordered = intervals.OrderBy(static interval => interval.Start).ToArray();
        if (ordered.Length is 0)
            return 0;
        var start = ordered[0].Start;
        var end = ordered[0].End;
        var total = TimeSpan.Zero;
        foreach (var interval in ordered.Skip(1))
        {
            if (interval.Start <= end)
            {
                if (interval.End > end)
                    end = interval.End;
                continue;
            }
            total += end - start;
            start = interval.Start;
            end = interval.End;
        }
        total += end - start;
        return Math.Max(0, total.TotalMilliseconds);
    }

    private static void UpdateLifecycle(
        MutableNode node,
        WorkflowEventStorageRow workflowEvent,
        string status,
        bool starts = false,
        bool ends = false)
    {
        node.Status = status;
        if (starts && (node.StartedAt is null || workflowEvent.Timestamp < node.StartedAt))
            node.StartedAt = workflowEvent.Timestamp;
        if (ends)
            node.EndedAt = workflowEvent.Timestamp;
        node.AddContent(workflowEvent.ContentRefs);
    }

    private static string? OwnerNodeId(
        WorkflowEventStorageRow workflowEvent,
        string? attemptId,
        string? attemptNodeId) =>
        workflowEvent.AgentId is null ? attemptNodeId : AgentNodeId(attemptId, workflowEvent.AgentId);

    private static string? NodeForEvent(WorkflowEventStorageRow workflowEvent, string? attemptId) =>
        workflowEvent.Kind switch
        {
            WorkflowJournalEventKind.AgentSpawned or
            WorkflowJournalEventKind.AgentStarted or
            WorkflowJournalEventKind.AgentCompleted when workflowEvent.AgentId is not null =>
                AgentNodeId(attemptId, workflowEvent.AgentId),
            WorkflowJournalEventKind.ToolStarted or
            WorkflowJournalEventKind.ToolCompleted when workflowEvent.ToolCallId is not null =>
                ToolNodeId(attemptId, workflowEvent.ToolCallId),
            WorkflowJournalEventKind.TurnStarted or
            WorkflowJournalEventKind.TurnCompleted or
            WorkflowJournalEventKind.TurnInterrupted when workflowEvent.TurnId is not null =>
                TurnNodeId(attemptId, workflowEvent.TurnId),
            WorkflowJournalEventKind.MessageSent or WorkflowJournalEventKind.MessageReceived =>
                NodeId("message", workflowEvent.EventId),
            WorkflowJournalEventKind.ItemStarted or WorkflowJournalEventKind.ItemCompleted =>
                NodeId("item", DataString(workflowEvent, "item_id") ?? workflowEvent.EventId),
            _ => null
        };

    private static string AgentNodeId(string? attemptId, string agentId) =>
        NodeId("agent", attemptId ?? "run", agentId);

    private static string ToolNodeId(string? attemptId, string toolCallId) =>
        NodeId("tool", attemptId ?? "run", toolCallId);

    private static string TurnNodeId(string? attemptId, string turnId) =>
        NodeId("turn", attemptId ?? "run", turnId);

    private static string EventLabel(WorkflowEventStorageRow workflowEvent, string fallback) =>
        DataString(workflowEvent, "label") ??
        DataString(workflowEvent, "name") ??
        DataString(workflowEvent, "tool_name") ??
        DataString(workflowEvent, "action") ??
        fallback;

    private static string EventStatus(WorkflowEventStorageRow workflowEvent, string fallback) =>
        DataString(workflowEvent, "status") ??
        DataString(workflowEvent, "outcome") ??
        fallback;

    private static string? DataString(WorkflowEventStorageRow workflowEvent, string name)
    {
        if (workflowEvent.DataJson is null)
            return null;
        using var document = JsonDocument.Parse(workflowEvent.DataJson);
        return document.RootElement.ValueKind is JsonValueKind.Object &&
               document.RootElement.TryGetProperty(name, out var value) &&
               value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static IReadOnlyDictionary<string, object>? ParseObject(string? json)
    {
        if (json is null)
            return null;
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind is not JsonValueKind.Object)
            return null;
        return document.RootElement.EnumerateObject()
            .ToDictionary(
                static property => property.Name,
                static property => (object)property.Value.Clone(),
                StringComparer.Ordinal);
    }

    private static string IdPart(string value) =>
        value.Contains(':', StringComparison.Ordinal) || value.Contains('\\', StringComparison.Ordinal)
            ? value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace(":", "\\c", StringComparison.Ordinal)
            : value;

    private static string NodeId(string kind, params string[] parts)
    {
        var builder = new StringBuilder(kind);
        foreach (var part in parts)
        {
            builder.Append(':');
            builder.Append(IdPart(part));
        }

        var composed = builder.ToString();
        return composed.Length <= MaxNodeIdLength
            ? composed
            : string.Concat(composed.AsSpan(0, MaxNodeIdLength - 17), "~", ShortHash(composed));
    }

    private static string ShortHash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];

    private static string RunStatus(WorkflowRunStatus status) => status switch
    {
        WorkflowRunStatus.Active => "active",
        WorkflowRunStatus.Completed => "completed",
        WorkflowRunStatus.Failed => "failed",
        WorkflowRunStatus.Interrupted => "interrupted",
        _ => "active"
    };

    private static void AddRecordedEdge(
        Dictionary<string, WorkflowGraphEdge> edges,
        string source,
        string target,
        WorkflowEdgeKind kind,
        string eventId)
    {
        if (source == target)
            return;
        var edgeId = $"{EdgeKind(kind)}:{source}:{target}";
        if (edges.TryGetValue(edgeId, out var existing) &&
            existing.Provenance is RecordedWorkflowEdgeProvenance recorded)
        {
            if (recorded.EventIds.Count >= MaxRecordedEventIds ||
                recorded.EventIds.Contains(eventId, StringComparer.Ordinal))
            {
                return;
            }

            edges[edgeId] = new WorkflowGraphEdge
            {
                EdgeId = edgeId,
                SourceNodeId = source,
                TargetNodeId = target,
                Kind = kind,
                Provenance = new RecordedWorkflowEdgeProvenance
                {
                    EventIds = [.. recorded.EventIds, eventId]
                }
            };
            return;
        }
        edges[edgeId] = new WorkflowGraphEdge
        {
            EdgeId = edgeId,
            SourceNodeId = source,
            TargetNodeId = target,
            Kind = kind,
            Provenance = new RecordedWorkflowEdgeProvenance { EventIds = [eventId] }
        };
    }

    private static string EdgeKind(WorkflowEdgeKind kind) => kind switch
    {
        WorkflowEdgeKind.Data => "data",
        WorkflowEdgeKind.Control => "control",
        WorkflowEdgeKind.Conflict => "conflict",
        WorkflowEdgeKind.Resource => "resource",
        WorkflowEdgeKind.Gate => "gate",
        WorkflowEdgeKind.Temporal => "temporal",
        _ => "temporal"
    };

    private sealed class MutableNode(
        string nodeId,
        WorkflowNodeKind kind,
        string label,
        string status,
        string? attemptId,
        string? agentId,
        string? parentNodeId,
        DateTimeOffset? startedAt,
        DateTimeOffset? endedAt,
        IReadOnlyList<string> contentRefs)
    {
        private readonly HashSet<string> _contentRefs = new(contentRefs, StringComparer.Ordinal);

        public string NodeId { get; } = nodeId;
        public WorkflowNodeKind Kind { get; } = kind;
        public string Label { get; } = label;
        public string Status { get; set; } = status;
        public string? AttemptId { get; } = attemptId;
        public string? AgentId { get; } = agentId;
        public string? ParentNodeId { get; } = parentNodeId;
        public DateTimeOffset? StartedAt { get; set; } = startedAt;
        public DateTimeOffset? EndedAt { get; set; } = endedAt;

        public void AddContent(IEnumerable<string> contentRefs)
        {
            foreach (var contentRef in contentRefs)
                _contentRefs.Add(contentRef);
        }

        public WorkflowGraphNode ToContract(DateTimeOffset now) =>
            new()
            {
                NodeId = NodeId,
                Kind = Kind,
                Label = Label,
                Status = Status,
                AttemptId = AttemptId,
                AgentId = AgentId,
                ParentNodeId = ParentNodeId,
                StartedAt = StartedAt,
                EndedAt = EndedAt,
                DurationMs = StartedAt.HasValue
                    ? Math.Max(0, ((EndedAt ?? now) - StartedAt.Value).TotalMilliseconds)
                    : null,
                ContentRefs = _contentRefs.Count is 0
                    ? null
                    : _contentRefs.Order(StringComparer.Ordinal).ToArray()
            };
    }
}
