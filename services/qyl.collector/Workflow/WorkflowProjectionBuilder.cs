using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Workflow;

internal static class WorkflowProjectionBuilder
{
    public const string SemanticFingerprint = "workflow-projector/2";

    private const int MaxRecordedEventIds = 32;

    private const int MaxConflictWitnessesPerPath = 32;

    private const int MaxIdentifierLength = 192;

    public static WorkflowProjectionCheckpoint BuildCheckpoint(
        WorkflowRunStorageRow run,
        WorkflowProjectionCheckpoint? prior,
        IReadOnlyList<WorkflowEventStorageRow> events,
        DateTimeOffset now,
        WorkflowProjectionBudget budget)
    {
        var runNodeId = NodeId("run", run.RunId);
        Dictionary<string, MutableNode> nodes;
        Dictionary<string, WorkflowGraphEdge> edges;
        string? activeAttempt;
        Dictionary<string, string> lastNodeByOwner;
        Dictionary<string, List<(string NodeId, string EventId)>> writesByPath;
        if (prior is null)
        {
            nodes = new Dictionary<string, MutableNode>(StringComparer.Ordinal)
            {
                [runNodeId] = new MutableNode(
                    runNodeId,
                    WorkflowNodeKind.Run,
                    run.Title ?? run.RunId,
                    RunStatus(run.Status),
                    null,
                    null,
                    run.StartedAt,
                    run.EndedAt,
                    [])
            };
            edges = new Dictionary<string, WorkflowGraphEdge>(StringComparer.Ordinal);
            activeAttempt = null;
            lastNodeByOwner = new Dictionary<string, string>(StringComparer.Ordinal);
            writesByPath =
                new Dictionary<string, List<(string NodeId, string EventId)>>(StringComparer.Ordinal);
        }
        else
        {
            if (prior.FormatVersion is not 2 ||
                prior.ProjectId != run.ProjectId ||
                prior.RunId != run.RunId ||
                prior.RunGeneration != run.RunGeneration ||
                prior.ProjectorSemanticFingerprint != SemanticFingerprint ||
                prior.ProjectionConfigurationFingerprint !=
                budget.Limits.ConfigurationFingerprint ||
                prior.RunInputHash != RunInputHash(run) ||
                prior.JournalSequence > run.LatestJournalSequence)
            {
                throw new InvalidDataException("Workflow projection checkpoint identity is invalid.");
            }

            nodes = prior.ReplayState.Nodes.ToDictionary(
                static node => node.NodeId,
                MutableNode.FromState,
                StringComparer.Ordinal);
            edges = prior.ReplayState.Edges.ToDictionary(
                static edge => edge.EdgeId,
                StringComparer.Ordinal);
            activeAttempt = prior.ReplayState.ActiveAttemptId;
            lastNodeByOwner = prior.ReplayState.OwnerCursors.ToDictionary(
                static cursor => cursor.OwnerNodeId,
                static cursor => cursor.NodeId,
                StringComparer.Ordinal);
            writesByPath = prior.ReplayState.PathWrites.ToDictionary(
                static path => path.PathKey,
                static path => path.Witnesses
                    .Select(static witness => (witness.NodeId, witness.EventId))
                    .ToList(),
                StringComparer.Ordinal);
            if (!nodes.TryGetValue(runNodeId, out var runNode))
                throw new InvalidDataException("Workflow projection checkpoint has no run node.");
            runNode.Status = RunStatus(run.Status);
            runNode.StartedAt = run.StartedAt;
            runNode.EndedAt = run.EndedAt;
        }

        var expectedSequence = (prior?.JournalSequence ?? 0) + 1;
        foreach (var workflowEvent in events.OrderBy(static item => item.JournalSequence))
        {
            if (workflowEvent.JournalSequence != expectedSequence)
                throw new InvalidDataException("Workflow projection journal suffix is not contiguous.");
            expectedSequence++;
            budget.ChargeWork();
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
                    ProjectFile(nodes, edges, writesByPath, workflowEvent, attemptId, attemptNodeId, budget);
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

            budget.EnsureGraphSize(nodes.Count, edges.Count);
        }
        if (expectedSequence - 1 != run.LatestJournalSequence)
            throw new InvalidDataException("Workflow projection journal suffix does not reach the requested run head.");

        var projectedNodes = nodes.Values
            .Select(node => node.ToContract(now))
            .OrderBy(static node => node.StartedAt)
            .ThenBy(static node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
        var projectedEdges = edges.Values
            .OrderBy(static edge => edge.EdgeId, StringComparer.Ordinal)
            .ToArray();
        budget.EnsureGraphSize(projectedNodes.Length, projectedEdges.Length);
        var statistics = CalculateStatistics(projectedNodes, projectedEdges, run, now, budget);

        var graph = new WorkflowGraphSnapshot
        {
            Run = ToContract(run with { MetadataJson = null }),
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
        var replayState = new WorkflowProjectionReplayState(
            activeAttempt,
            nodes.Values
                .OrderBy(static node => node.NodeId, StringComparer.Ordinal)
                .Select(static node => node.ToState())
                .ToArray(),
            projectedEdges,
            lastNodeByOwner
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => new WorkflowProjectionOwnerCursor(pair.Key, pair.Value))
                .ToArray(),
            writesByPath
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => new WorkflowProjectionPathWrites(
                    pair.Key,
                    pair.Value.Select(static witness =>
                            new WorkflowProjectionWriteWitness(witness.NodeId, witness.EventId))
                        .ToArray()))
                .ToArray());
        return new WorkflowProjectionCheckpoint(
            2,
            run.ProjectId,
            run.RunId,
            run.RunGeneration,
            SemanticFingerprint,
            budget.Limits.ConfigurationFingerprint,
            prior?.RunInputHash ?? RunInputHash(run),
            run.LatestJournalSequence,
            now,
            replayState,
            graph);
    }

    internal static string RunInputHash(WorkflowRunStorageRow run) =>
        FullHash(CanonicalTuple(
            "run-input",
            run.ProjectId,
            run.RunId,
            run.ThreadId is null ? "thread:null" : $"thread:value:{run.ThreadId}",
            run.Title is null ? "title:null" : $"title:value:{run.Title}",
            run.StartedAt.ToString("O"),
            run.MetadataJson is null ? "metadata:null" : $"metadata:value:{run.MetadataJson}"));

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
        var nodeId = ScopedNodeId("wait", attemptId, stableId);
        if (!nodes.TryGetValue(nodeId, out var node))
        {
            node = new MutableNode(
                nodeId,
                WorkflowNodeKind.Wait,
                EventLabel(workflowEvent, "Wait"),
                "waiting",
                attemptId,
                workflowEvent.AgentId,
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
        var commandId = DataString(workflowEvent, "command_id");
        var approvalId = DataString(workflowEvent, "approval_id");
        var domain = commandId is not null
            ? "command"
            : approvalId is not null
                ? "approval"
                : "event";
        var stableId = commandId ?? approvalId ?? workflowEvent.EventId;
        var nodeId = ScopedNodeId("gate", attemptId, domain, stableId);
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
        string? attemptNodeId,
        WorkflowProjectionBudget budget)
    {
        var path = DataString(workflowEvent, "path");
        var owner = OwnerNodeId(workflowEvent, attemptId, attemptNodeId);
        if (path is null || owner is null)
            return;

        var resourceId = NodeId("resource", "file", FullHash(path));
        if (!nodes.ContainsKey(resourceId))
        {
            nodes.Add(resourceId, new MutableNode(
                resourceId,
                WorkflowNodeKind.Resource,
                path,
                "written",
                null,
                null,
                workflowEvent.Timestamp,
                workflowEvent.Timestamp,
                workflowEvent.ContentRefs));
        }
        AddRecordedEdge(edges, owner, resourceId, WorkflowEdgeKind.Resource, workflowEvent.EventId);

        var attemptPath = attemptId is null
            ? CanonicalTuple("file-write", "run", path)
            : CanonicalTuple("file-write", "attempt", attemptId, path);
        if (!writesByPath.TryGetValue(attemptPath, out var previousWrites))
        {
            previousWrites = [];
            writesByPath.Add(attemptPath, previousWrites);
        }
        foreach (var previous in previousWrites)
        {
            budget.ChargeWork();
            if (previous.NodeId == owner)
                continue;
            var source = string.CompareOrdinal(previous.NodeId, owner) <= 0 ? previous.NodeId : owner;
            var target = source == owner ? previous.NodeId : owner;
            var edgeId = BoundedIdentifier("conflict", "file", path, source, target);
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
        var nodeId = ScopedNodeId(
            "item",
            attemptId,
            DataString(workflowEvent, "item_id") ?? workflowEvent.EventId);
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
        DateTimeOffset now,
        WorkflowProjectionBudget budget)
    {
        var nodeById = nodes.ToDictionary(static node => node.NodeId, StringComparer.Ordinal);
        var outgoing = new Dictionary<string, List<WorkflowGraphEdge>>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            budget.ChargeWork();
            if (!outgoing.TryGetValue(edge.SourceNodeId, out var targets))
            {
                targets = [];
                outgoing.Add(edge.SourceNodeId, targets);
            }
            targets.Add(edge);
        }

        var fragments = new List<WorkFragment>();
        foreach (var node in nodes)
        {
            budget.ChargeWork();
            if (!IsWeighted(node) || node.StartedAt is null)
                continue;

            var interval = Interval(node.StartedAt.Value, node.EndedAt ?? now);
            if (node.Kind is not WorkflowNodeKind.Agent)
            {
                AddPositiveFragment(fragments, node.NodeId, interval);
                continue;
            }

            var ownedChildren = new List<(DateTimeOffset Start, DateTimeOffset End)>();
            if (outgoing.TryGetValue(node.NodeId, out var candidates))
            {
                foreach (var edge in candidates)
                {
                    budget.ChargeWork();
                    if (!nodeById.TryGetValue(edge.TargetNodeId, out var child) ||
                        child.StartedAt is null ||
                        !IsPreciselyOwnedChild(edge.Kind, child))
                    {
                        continue;
                    }

                    var clipped = Clip(
                        Interval(child.StartedAt.Value, child.EndedAt ?? now),
                        interval);
                    if (clipped.HasValue)
                        ownedChildren.Add(clipped.Value);
                }
            }

            foreach (var fragment in Subtract(interval, ownedChildren))
                AddPositiveFragment(fragments, node.NodeId, fragment);
        }

        var peakConcurrency = PeakConcurrency(fragments);
        var distinctAgents = nodes
            .Where(static node => node.Kind is WorkflowNodeKind.Agent && node.AgentId is not null)
            .Select(static node => node.AgentId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var workerCount = Math.Max(
            Math.Max(1, peakConcurrency),
            distinctAgents);
        var weights = nodes.ToDictionary(static node => node.NodeId, static _ => 0d, StringComparer.Ordinal);
        foreach (var fragment in fragments)
            weights[fragment.NodeId] += (fragment.End - fragment.Start).TotalMilliseconds;
        var t1 = weights.Sum(static pair => pair.Value);
        var (tInfinity, criticalPath) = LongestPath(nodes, edges, weights, budget);
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
        IReadOnlyDictionary<string, double> weights,
        WorkflowProjectionBudget budget)
    {
        var ordered = nodes
            .OrderBy(static node => node.NodeId, StringComparer.Ordinal)
            .Select(static node => node.NodeId)
            .ToArray();
        var known = ordered.ToHashSet(StringComparer.Ordinal);
        var dependencies = edges
            .Where(static edge => edge.Kind is
                WorkflowEdgeKind.Data or WorkflowEdgeKind.Control or WorkflowEdgeKind.Gate)
            .Where(edge => known.Contains(edge.SourceNodeId) && known.Contains(edge.TargetNodeId))
            .Where(static edge => !string.Equals(edge.SourceNodeId, edge.TargetNodeId, StringComparison.Ordinal))
            .Select(static edge => (Source: edge.SourceNodeId, Target: edge.TargetNodeId))
            .Distinct()
            .ToArray();
        var forward = BuildAdjacency(ordered, dependencies, reverse: false);
        var reverse = BuildAdjacency(ordered, dependencies, reverse: true);
        var finishOrder = FinishOrder(ordered, forward, budget);
        var components = StronglyConnectedComponents(finishOrder, reverse, budget);
        if (components.Count is 0)
            return (0, []);

        var componentByNode = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var component = 0; component < components.Count; component++)
        {
            foreach (var nodeId in components[component])
                componentByNode.Add(nodeId, component);
        }

        var componentKeys = components.Select(static component => component[0]).ToArray();
        var componentWeights = components
            .Select(component => component.Sum(nodeId => weights.GetValueOrDefault(nodeId)))
            .ToArray();
        var successors = Enumerable.Range(0, components.Count)
            .Select(static _ => new HashSet<int>())
            .ToArray();
        var predecessors = Enumerable.Range(0, components.Count)
            .Select(static _ => new HashSet<int>())
            .ToArray();
        foreach (var (source, target) in dependencies)
        {
            budget.ChargeWork();
            var sourceComponent = componentByNode[source];
            var targetComponent = componentByNode[target];
            if (sourceComponent == targetComponent || !successors[sourceComponent].Add(targetComponent))
                continue;
            predecessors[targetComponent].Add(sourceComponent);
        }

        var inDegree = predecessors.Select(static items => items.Count).ToArray();
        var componentByKey = Enumerable.Range(0, components.Count)
            .ToDictionary(component => componentKeys[component], StringComparer.Ordinal);
        var ready = new SortedSet<string>(StringComparer.Ordinal);
        for (var component = 0; component < components.Count; component++)
        {
            if (inDegree[component] is 0)
                ready.Add(componentKeys[component]);
        }

        var topological = new List<int>(components.Count);
        while (ready.Count > 0)
        {
            var key = ready.Min!;
            ready.Remove(key);
            var component = componentByKey[key];
            topological.Add(component);
            foreach (var successor in successors[component]
                         .OrderBy(item => componentKeys[item], StringComparer.Ordinal))
            {
                budget.ChargeWork();
                if (--inDegree[successor] is 0)
                    ready.Add(componentKeys[successor]);
            }
        }

        var scores = new double[components.Count];
        var previous = Enumerable.Repeat(-1, components.Count).ToArray();
        foreach (var component in topological)
        {
            var bestScore = 0d;
            var bestPrevious = -1;
            foreach (var candidate in predecessors[component]
                         .OrderBy(item => componentKeys[item], StringComparer.Ordinal))
            {
                budget.ChargeWork();
                if (bestPrevious < 0 ||
                    scores[candidate] > bestScore ||
                    scores[candidate] == bestScore &&
                    string.CompareOrdinal(componentKeys[candidate], componentKeys[bestPrevious]) < 0)
                {
                    bestScore = scores[candidate];
                    bestPrevious = candidate;
                }
            }
            scores[component] = bestScore + componentWeights[component];
            previous[component] = bestPrevious;
        }

        var end = Enumerable.Range(0, components.Count)
            .OrderByDescending(component => scores[component])
            .ThenBy(component => componentKeys[component], StringComparer.Ordinal)
            .First();
        var componentPath = new List<int>();
        for (var cursor = end; cursor >= 0; cursor = previous[cursor])
            componentPath.Add(cursor);
        componentPath.Reverse();
        var path = componentPath.SelectMany(component => components[component]).ToArray();
        return (scores[end], path);
    }

    private static Dictionary<string, string[]> BuildAdjacency(
        IReadOnlyList<string> nodes,
        IReadOnlyList<(string Source, string Target)> dependencies,
        bool reverse)
    {
        var adjacency = nodes.ToDictionary(
            static nodeId => nodeId,
            static _ => new List<string>(),
            StringComparer.Ordinal);
        foreach (var (source, target) in dependencies)
            adjacency[reverse ? target : source].Add(reverse ? source : target);
        return adjacency.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);
    }

    private static List<string> FinishOrder(
        IReadOnlyList<string> ordered,
        IReadOnlyDictionary<string, string[]> adjacency,
        WorkflowProjectionBudget budget)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var finished = new List<string>(ordered.Count);
        foreach (var root in ordered)
        {
            if (!visited.Add(root))
                continue;
            budget.ChargeWork();
            var stack = new Stack<(string NodeId, int NextIndex)>();
            stack.Push((root, 0));
            while (stack.Count > 0)
            {
                var (nodeId, nextIndex) = stack.Pop();
                var targets = adjacency[nodeId];
                if (nextIndex >= targets.Length)
                {
                    finished.Add(nodeId);
                    continue;
                }

                stack.Push((nodeId, nextIndex + 1));
                budget.ChargeWork();
                if (visited.Add(targets[nextIndex]))
                {
                    budget.ChargeWork();
                    stack.Push((targets[nextIndex], 0));
                }
            }
        }
        return finished;
    }

    private static List<string[]> StronglyConnectedComponents(
        IReadOnlyList<string> finishOrder,
        IReadOnlyDictionary<string, string[]> reverse,
        WorkflowProjectionBudget budget)
    {
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<string[]>();
        for (var index = finishOrder.Count - 1; index >= 0; index--)
        {
            var root = finishOrder[index];
            if (!assigned.Add(root))
                continue;
            var members = new List<string>();
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.TryPop(out var nodeId))
            {
                budget.ChargeWork();
                members.Add(nodeId);
                foreach (var source in reverse[nodeId])
                {
                    budget.ChargeWork();
                    if (assigned.Add(source))
                        stack.Push(source);
                }
            }
            members.Sort(StringComparer.Ordinal);
            components.Add(members.ToArray());
        }
        components.Sort(static (left, right) => string.CompareOrdinal(left[0], right[0]));
        return components;
    }

    private static bool IsWeighted(WorkflowGraphNode node) =>
        node.Kind is WorkflowNodeKind.Agent
            or WorkflowNodeKind.ToolCall
            or WorkflowNodeKind.Wait
            or WorkflowNodeKind.Gate
            or WorkflowNodeKind.Message;

    private static bool IsPreciselyOwnedChild(WorkflowEdgeKind edgeKind, WorkflowGraphNode child) =>
        edgeKind switch
        {
            WorkflowEdgeKind.Control => child.Kind is WorkflowNodeKind.Agent or WorkflowNodeKind.ToolCall,
            WorkflowEdgeKind.Temporal => child.Kind is WorkflowNodeKind.Wait,
            WorkflowEdgeKind.Gate => child.Kind is WorkflowNodeKind.Gate,
            WorkflowEdgeKind.Data => child.Kind is WorkflowNodeKind.Message && child.DurationMs > 0,
            _ => false
        };

    private static void AddPositiveFragment(
        ICollection<WorkFragment> fragments,
        string nodeId,
        (DateTimeOffset Start, DateTimeOffset End) interval)
    {
        if (interval.End > interval.Start)
            fragments.Add(new WorkFragment(nodeId, interval.Start, interval.End));
    }

    private static int PeakConcurrency(IReadOnlyList<WorkFragment> fragments)
    {
        var points = fragments
            .SelectMany(static fragment => new[] { (fragment.Start, 1), (fragment.End, -1) })
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

    private static (DateTimeOffset Start, DateTimeOffset End) Interval(
        DateTimeOffset start,
        DateTimeOffset end) =>
        (start, end < start ? start : end);

    private static (DateTimeOffset Start, DateTimeOffset End)? Clip(
        (DateTimeOffset Start, DateTimeOffset End) interval,
        (DateTimeOffset Start, DateTimeOffset End) bounds)
    {
        var start = interval.Start < bounds.Start ? bounds.Start : interval.Start;
        var end = interval.End > bounds.End ? bounds.End : interval.End;
        return end > start ? (start, end) : null;
    }

    private static IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> Subtract(
        (DateTimeOffset Start, DateTimeOffset End) parent,
        IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> children)
    {
        var merged = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        foreach (var child in children.OrderBy(static interval => interval.Start)
                     .ThenBy(static interval => interval.End))
        {
            if (merged.Count is 0 || child.Start > merged[^1].End)
            {
                merged.Add(child);
                continue;
            }
            if (child.End > merged[^1].End)
                merged[^1] = (merged[^1].Start, child.End);
        }

        var fragments = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        var cursor = parent.Start;
        foreach (var child in merged)
        {
            if (child.Start > cursor)
                fragments.Add((cursor, child.Start));
            if (child.End > cursor)
                cursor = child.End;
        }
        if (cursor < parent.End)
            fragments.Add((cursor, parent.End));
        return fragments;
    }

    private readonly record struct WorkFragment(
        string NodeId,
        DateTimeOffset Start,
        DateTimeOffset End);

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
                ScopedNodeId(
                    "item",
                    attemptId,
                    DataString(workflowEvent, "item_id") ?? workflowEvent.EventId),
            _ => null
        };

    private static string AgentNodeId(string? attemptId, string agentId) =>
        ScopedNodeId("agent", attemptId, agentId);

    private static string ToolNodeId(string? attemptId, string toolCallId) =>
        ScopedNodeId("tool", attemptId, toolCallId);

    private static string TurnNodeId(string? attemptId, string turnId) =>
        ScopedNodeId("turn", attemptId, turnId);

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

    private static string ScopedNodeId(string kind, string? attemptId, params string[] stableParts)
    {
        var scope = attemptId is null
            ? new[] { "run" }
            : new[] { "attempt", attemptId };
        return NodeId(kind, [.. scope, .. stableParts]);
    }

    private static string NodeId(string kind, params string[] parts) =>
        BoundedIdentifier(kind, parts);

    private static string BoundedIdentifier(string kind, params string[] parts)
    {
        var builder = new StringBuilder(kind);
        foreach (var part in parts)
        {
            builder.Append(':');
            builder.Append(IdPart(part));
        }

        var composed = builder.ToString();
        return composed.EnumerateRunes().Count() <= MaxIdentifierLength
            ? composed
            : $"{kind}~{FullHash(CanonicalTuple(kind, parts))}";
    }

    private static string CanonicalTuple(string kind, params string[] parts)
    {
        var builder = new StringBuilder();
        AppendCanonicalPart(builder, kind);
        foreach (var part in parts)
            AppendCanonicalPart(builder, part);
        return builder.ToString();
    }

    private static void AppendCanonicalPart(StringBuilder builder, string value)
    {
        builder.Append(Encoding.UTF8.GetByteCount(value));
        builder.Append('#');
        builder.Append(value);
    }

    private static string FullHash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

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
        var edgeId = BoundedIdentifier(EdgeKind(kind), source, target);
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
        public DateTimeOffset? StartedAt { get; set; } = startedAt;
        public DateTimeOffset? EndedAt { get; set; } = endedAt;

        public static MutableNode FromState(WorkflowProjectionNodeState state) =>
            new(
                state.NodeId,
                state.Kind,
                state.Label,
                state.Status,
                state.AttemptId,
                state.AgentId,
                state.StartedAt,
                state.EndedAt,
                state.ContentRefs);

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
                StartedAt = StartedAt,
                EndedAt = EndedAt,
                DurationMs = StartedAt.HasValue
                    ? Math.Max(0, ((EndedAt ?? now) - StartedAt.Value).TotalMilliseconds)
                    : null,
                ContentRefs = _contentRefs.Count is 0
                    ? null
                    : _contentRefs.Order(StringComparer.Ordinal).ToArray()
            };

        public WorkflowProjectionNodeState ToState() =>
            new(
                NodeId,
                Kind,
                Label,
                Status,
                AttemptId,
                AgentId,
                StartedAt,
                EndedAt,
                _contentRefs.Order(StringComparer.Ordinal).ToArray());
    }
}
