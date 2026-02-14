// =============================================================================
// qyl.copilot - DAG Scheduler
// Node dependency resolution and parallel execution engine
// =============================================================================

using System.Collections.Concurrent;
using Microsoft.Agents.AI;
using qyl.copilot.Adapters;

namespace qyl.copilot.Workflows;

/// <summary>
///     A node in a workflow DAG.
/// </summary>
public sealed record WorkflowNode
{
    /// <summary>Unique node identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable node name.</summary>
    public required string Name { get; init; }

    /// <summary>Node type (e.g., "action", "decision", "parallel").</summary>
    public string Type { get; init; } = "action";

    /// <summary>IDs of nodes this node depends on (must complete before this node runs).</summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <summary>IDs of nodes that depend on this node (populated during graph build).</summary>
    public IReadOnlyList<string> Dependents { get; init; } = [];

    /// <summary>Handler to execute when this node runs.</summary>
    public Func<DagExecutionContext, CancellationToken, Task<object?>>? Handler { get; init; }

    /// <summary>Conditional edges for branching (evaluated by ConditionalRouter).</summary>
    public IReadOnlyList<ConditionalEdge>? ConditionalEdges { get; init; }

    /// <summary>Optional agent name for nodes that invoke an AI agent via <see cref="QylAgentPipeline"/>.</summary>
    public string? AgentName { get; init; }

    /// <summary>Optional agent instructions for agent-backed nodes.</summary>
    public string? AgentInstructions { get; init; }
}

/// <summary>
///     Workflow definition as a DAG of nodes.
/// </summary>
public sealed record WorkflowDefinition
{
    /// <summary>Workflow name.</summary>
    public required string Name { get; init; }

    /// <summary>All nodes in the DAG.</summary>
    public required IReadOnlyList<WorkflowNode> Nodes { get; init; }
}

/// <summary>
///     Context passed to each node during DAG execution.
/// </summary>
public sealed class DagExecutionContext
{
    /// <summary>Unique execution ID.</summary>
    public required string ExecutionId { get; init; }

    /// <summary>Shared state store for this execution run.</summary>
    public required SharedStateStore SharedState { get; init; }

    /// <summary>Checkpoint manager for durable boundaries.</summary>
    public required CheckpointManager CheckpointManager { get; init; }

    /// <summary>Event stream for execution events.</summary>
    public required WorkflowEventStream EventStream { get; init; }

    /// <summary>The node currently being executed.</summary>
    public WorkflowNode? CurrentNode { get; internal set; }

    /// <summary>Results from completed nodes keyed by node ID.</summary>
    public ConcurrentDictionary<string, object?> NodeResults { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Optional service provider for resolving agent dependencies.</summary>
    public IServiceProvider? Services { get; init; }

    /// <summary>Agent threads keyed by thread ID for cross-checkpoint continuity.</summary>
    public ConcurrentDictionary<string, QylAgentThread> AgentThreads { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
///     Result of a DAG execution.
/// </summary>
public sealed record DagExecutionResult
{
    /// <summary>Nodes that completed successfully.</summary>
    public required IReadOnlyList<string> CompletedNodes { get; init; }

    /// <summary>Nodes that failed, with error messages.</summary>
    public required IReadOnlyDictionary<string, string> FailedNodes { get; init; }

    /// <summary>Nodes that were skipped (due to upstream failures).</summary>
    public required IReadOnlyList<string> SkippedNodes { get; init; }

    /// <summary>Combined output from all completed nodes.</summary>
    public required IReadOnlyDictionary<string, object?> Output { get; init; }

    /// <summary>Whether all nodes completed successfully.</summary>
    public bool Success => FailedNodes.Count == 0 && SkippedNodes.Count == 0;
}

/// <summary>
///     Resolves DAG execution order and runs nodes in parallel where possible.
///     Supports fan-out (parallel independent nodes) and fan-in (wait for all dependencies).
/// </summary>
public sealed class DagScheduler
{
    private readonly ConditionalRouter _router;
    private readonly ILogger _logger;
    private readonly int _maxWorkers;

    /// <summary>
    ///     Creates a new DAG scheduler.
    /// </summary>
    /// <param name="router">Conditional router for branching decisions.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="maxWorkers">Maximum concurrent node executions (default: 8).</param>
    public DagScheduler(ConditionalRouter router, ILogger<DagScheduler> logger, int maxWorkers = 8)
    {
        _router = Guard.NotNull(router);
        _logger = Guard.NotNull(logger);
        _maxWorkers = maxWorkers > 0 ? maxWorkers : 8;
    }

    /// <summary>
    ///     Executes a workflow DAG, resolving dependencies and running nodes in parallel.
    /// </summary>
    public async Task<DagExecutionResult> ExecuteAsync(
        WorkflowDefinition workflow,
        DagExecutionContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(context);

        var nodeMap = workflow.Nodes.ToDictionary(static n => n.Id, StringComparer.OrdinalIgnoreCase);
        ValidateDag(nodeMap);

        var completed = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var failed = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var skipped = new ConcurrentBag<string>();

        // Track in-degree for topological scheduling
        var inDegree = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in workflow.Nodes)
        {
            inDegree[node.Id] = node.Dependencies.Count;
        }

        // Find initial ready nodes (no dependencies)
        var readyQueue = new ConcurrentQueue<string>();
        foreach (var node in workflow.Nodes)
        {
            if (node.Dependencies.Count == 0)
                readyQueue.Enqueue(node.Id);
        }

        using var semaphore = new SemaphoreSlim(_maxWorkers, _maxWorkers);
        var pendingTasks = new ConcurrentDictionary<string, Task>(StringComparer.OrdinalIgnoreCase);
        var allDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        int totalNodes = workflow.Nodes.Count;
        int processedCount = 0;

        // Process nodes as they become ready
        await ProcessReadyNodesAsync();

        // Wait for all in-flight tasks
        while (!pendingTasks.IsEmpty)
        {
            await Task.WhenAll(pendingTasks.Values).ConfigureAwait(false);
        }

        // Any nodes not completed or failed are skipped
        foreach (var node in workflow.Nodes)
        {
            if (!completed.ContainsKey(node.Id) && !failed.ContainsKey(node.Id))
                skipped.Add(node.Id);
        }

        return new DagExecutionResult
        {
            CompletedNodes = completed.Keys.ToList(),
            FailedNodes = failed.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase),
            SkippedNodes = [.. skipped],
            Output = context.NodeResults.ToDictionary(
                kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
        };

        async Task ProcessReadyNodesAsync()
        {
            while (readyQueue.TryDequeue(out var nodeId))
            {
                ct.ThrowIfCancellationRequested();

                if (!nodeMap.TryGetValue(nodeId, out var node))
                    continue;

                // Skip if any dependency failed
                bool hasFailedDep = false;
                foreach (var dep in node.Dependencies)
                {
                    if (failed.ContainsKey(dep))
                    {
                        hasFailedDep = true;
                        break;
                    }
                }

                if (hasFailedDep)
                {
                    skipped.Add(nodeId);
                    Interlocked.Increment(ref processedCount);
                    UnblockDependents(nodeId, nodeMap, inDegree, readyQueue, failed: true);
                    continue;
                }

                await semaphore.WaitAsync(ct).ConfigureAwait(false);

                var capturedNodeId = nodeId;
                var capturedNode = node;
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await ExecuteNodeAsync(capturedNode, context, ct).ConfigureAwait(false);
                        completed[capturedNodeId] = true;

                        // Resolve conditional routing
                        context.NodeResults.TryGetValue(capturedNodeId, out var output);
                        var nextNodes = _router.ResolveNextNodes(capturedNode, output, context.SharedState.Snapshot());

                        UnblockDependents(capturedNodeId, nodeMap, inDegree, readyQueue, failed: false, nextNodes);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        failed[capturedNodeId] = "Cancelled";
                        UnblockDependents(capturedNodeId, nodeMap, inDegree, readyQueue, failed: true);
                    }
                    catch (Exception ex)
                    {
                        failed[capturedNodeId] = ex.Message;
                        _logger.LogError(ex, "DAG node {NodeId} failed", capturedNodeId);
                        UnblockDependents(capturedNodeId, nodeMap, inDegree, readyQueue, failed: true);
                    }
                    finally
                    {
                        Interlocked.Increment(ref processedCount);
                        semaphore.Release();
                        pendingTasks.TryRemove(capturedNodeId, out _);

                        // Process any newly ready nodes
                        await ProcessReadyNodesAsync().ConfigureAwait(false);
                    }
                }, ct);

                pendingTasks[capturedNodeId] = task;
            }
        }
    }

    private async Task ExecuteNodeAsync(WorkflowNode node, DagExecutionContext context, CancellationToken ct)
    {
        context.CurrentNode = node;

        await context.EventStream.AppendAsync(new WorkflowEvent
        {
            ExecutionId = context.ExecutionId,
            Type = WorkflowEventType.NodeStarted,
            NodeId = node.Id,
            Timestamp = TimeProvider.System.GetUtcNow()
        }, ct).ConfigureAwait(false);

        _logger.LogDebug("DAG executing node {NodeId} ({NodeName})", node.Id, node.Name);

        object? result = null;
        try
        {
            if (node.AgentName is not null && context.Services is not null)
            {
                result = await ExecuteAgentNodeAsync(node, context, ct).ConfigureAwait(false);
            }
            else if (node.Handler is not null)
            {
                result = await node.Handler(context, ct).ConfigureAwait(false);
            }

            context.NodeResults[node.Id] = result;

            // Checkpoint after every node completion
            await context.CheckpointManager.SaveCheckpointAsync(
                context.ExecutionId, node.Id, result, ct).ConfigureAwait(false);

            // Save agent thread state if present
            if (context.AgentThreads.TryGetValue(node.Id, out var thread))
            {
                await thread.SaveToCheckpointAsync(
                    context.CheckpointManager, context.ExecutionId, ct).ConfigureAwait(false);
            }

            await context.EventStream.AppendAsync(new WorkflowEvent
            {
                ExecutionId = context.ExecutionId,
                Type = WorkflowEventType.NodeCompleted,
                NodeId = node.Id,
                Data = result,
                Timestamp = TimeProvider.System.GetUtcNow()
            }, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await context.EventStream.AppendAsync(new WorkflowEvent
            {
                ExecutionId = context.ExecutionId,
                Type = WorkflowEventType.NodeFailed,
                NodeId = node.Id,
                Data = ex.Message,
                Timestamp = TimeProvider.System.GetUtcNow()
            }, ct).ConfigureAwait(false);

            throw;
        }
    }

    private static async Task<string> ExecuteAgentNodeAsync(WorkflowNode node, DagExecutionContext context, CancellationToken ct)
    {
        var agent = QylAgentPipeline.CreateInstrumented(node.AgentName!, context.Services!)
            .Build(context.Services!);

        // Build prompt from node instructions and upstream results
        var prompt = node.AgentInstructions ?? node.Name;
        if (node.Dependencies.Count > 0)
        {
            var upstreamContext = new System.Text.StringBuilder();
            upstreamContext.AppendLine("## Upstream Results");
            foreach (var depId in node.Dependencies)
            {
                if (context.NodeResults.TryGetValue(depId, out var depResult) && depResult is not null)
                {
                    upstreamContext.AppendLine($"### {depId}");
                    upstreamContext.AppendLine(depResult.ToString());
                }
            }

            prompt = $"{prompt}\n\n{upstreamContext}";
        }

        // Create session from agent (InMemoryAgentSession is abstract, use agent factory)
        var session = await agent.CreateSessionAsync(cancellationToken: ct).ConfigureAwait(false);

        // Stream agent response into workflow event stream
        var responseBuilder = new System.Text.StringBuilder();
        await foreach (var update in agent.RunStreamingAsync(prompt, session, cancellationToken: ct).ConfigureAwait(false))
        {
            var content = update.ToString();
            if (!string.IsNullOrEmpty(content))
            {
                responseBuilder.Append(content);

                await context.EventStream.AppendAsync(new WorkflowEvent
                {
                    ExecutionId = context.ExecutionId,
                    Type = WorkflowEventType.StateUpdated,
                    NodeId = node.Id,
                    Data = content,
                    Timestamp = TimeProvider.System.GetUtcNow()
                }, ct).ConfigureAwait(false);
            }
        }

        return responseBuilder.ToString();
    }

    private static void UnblockDependents(
        string completedNodeId,
        Dictionary<string, WorkflowNode> nodeMap,
        ConcurrentDictionary<string, int> inDegree,
        ConcurrentQueue<string> readyQueue,
        bool failed,
        IReadOnlyList<string>? conditionalNext = null)
    {
        // If conditional routing returned specific next nodes, only unblock those
        var candidates = conditionalNext is { Count: > 0 }
            ? conditionalNext
            : nodeMap.Values
                .Where(n => n.Dependencies.Contains(completedNodeId))
                .Select(static n => n.Id)
                .ToList();

        foreach (var candidateId in candidates)
        {
            if (!nodeMap.ContainsKey(candidateId))
                continue;

            if (failed)
            {
                // Propagate failure — mark dependents to skip
                continue;
            }

            var remaining = inDegree.AddOrUpdate(candidateId, 0, static (_, current) => current - 1);
            if (remaining <= 0)
            {
                readyQueue.Enqueue(candidateId);
            }
        }
    }

    private static void ValidateDag(Dictionary<string, WorkflowNode> nodeMap)
    {
        // Topological sort cycle detection using DFS
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var nodeId in nodeMap.Keys)
        {
            if (!visited.Contains(nodeId))
                DetectCycle(nodeId, nodeMap, visiting, visited);
        }
    }

    private static void DetectCycle(
        string nodeId,
        Dictionary<string, WorkflowNode> nodeMap,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (visiting.Contains(nodeId))
            throw new InvalidOperationException($"Cycle detected in workflow DAG at node '{nodeId}'.");

        if (visited.Contains(nodeId))
            return;

        visiting.Add(nodeId);

        if (nodeMap.TryGetValue(nodeId, out var node))
        {
            foreach (var dep in node.Dependencies)
            {
                DetectCycle(dep, nodeMap, visiting, visited);
            }
        }

        visiting.Remove(nodeId);
        visited.Add(nodeId);
    }
}
