using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Qyl.Api.Contracts.Workflow;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

/// <summary>
/// Work-span invariants for the graph projection. These assert properties that must hold for
/// EVERY journal rather than golden numbers for one: a statistic that only happens to be right
/// on a fixture is the failure mode this whole surface exists to catch.
/// </summary>
public sealed class WorkflowGraphInvariantTests
{
    private static readonly DateTimeOffset s_startedAt =
        new(2026, 7, 28, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The span is a path through the work, so it can never exceed the work. Wait nodes used
    /// to be dropped from T1 while LongestPath still charged their weight, which pushed
    /// tInfinityMs above t1Ms and made parallelLowerBoundMs claim a floor above fully serial
    /// execution — a graph asserting you cannot beat a time slower than doing nothing at once.
    /// </summary>
    [Fact]
    public async Task Span_never_exceeds_total_work_even_when_a_wait_is_on_the_critical_path()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);

        await store.AppendWorkflowEventsAsync(
            "project-a", "run-1", "observer-1",
            [
                Event("attempt", 1, WorkflowJournalEventKind.AttemptStarted, attemptId: "attempt-1"),
                Event("root-spawn", 2, WorkflowJournalEventKind.AgentSpawned, 0, "attempt-1", "root"),
                Event("root-start", 3, WorkflowJournalEventKind.AgentStarted, 0, "attempt-1", "root"),
                Event("w1-spawn", 4, WorkflowJournalEventKind.AgentSpawned, 1, "attempt-1", "worker-1",
                    parentAgentId: "root"),
                Event("w1-start", 5, WorkflowJournalEventKind.AgentStarted, 1, "attempt-1", "worker-1",
                    parentAgentId: "root"),
                Event("wait-start", 6, WorkflowJournalEventKind.WaitStarted, 2, "attempt-1", "worker-1"),
                Event("wait-done", 7, WorkflowJournalEventKind.WaitCompleted, 900, "attempt-1", "worker-1"),
                Event("w1-end", 8, WorkflowJournalEventKind.AgentCompleted, 950, "attempt-1", "worker-1"),
                Event("root-end", 9, WorkflowJournalEventKind.AgentCompleted, 960, "attempt-1", "root"),
                Event("attempt-end", 10, WorkflowJournalEventKind.AttemptCompleted, 960, "attempt-1")
            ],
            [], TestContext.Current.CancellationToken);

        var graph = await ReadGraphAsync(store);
        var statistics = graph.Statistics;

        Assert.True(
            statistics.TInfinityMs <= statistics.T1Ms,
            $"span {statistics.TInfinityMs}ms exceeded work {statistics.T1Ms}ms — " +
            "T1 and T-infinity are not summed over the same weight function");
    }

    /// <summary>
    /// Brent's bound is only meaningful when it is actually the max of its two terms, and both
    /// terms must be non-negative for any journal.
    /// </summary>
    [Fact]
    public async Task Brent_lower_bound_is_the_max_of_its_two_terms()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);
        await store.AppendWorkflowEventsAsync(
            "project-a", "run-1", "observer-1", FanOutJournal(messageBetweenWorkers: false),
            [], TestContext.Current.CancellationToken);

        var statistics = (await ReadGraphAsync(store)).Statistics;

        Assert.True(statistics.WorkerCount >= 1, "workerCount violates its @minValue(1) contract");
        Assert.True(statistics.PeakConcurrency >= 0, "peakConcurrency went negative");
        Assert.True(statistics.T1Ms >= 0 && statistics.TInfinityMs >= 0, "negative work or span");
        Assert.Equal(
            Math.Max(statistics.T1Ms / statistics.WorkerCount, statistics.TInfinityMs),
            statistics.ParallelLowerBoundMs);
        Assert.True(statistics.ParallelLowerBoundMs >= statistics.TInfinityMs);
    }

    /// <summary>
    /// A message from one agent to another is the only representation of cross-agent causality
    /// (a Data edge). It was excluded from the span while Temporal correlation edges were
    /// included, so the critical path was computed over the wrong edge set in both directions.
    /// Two journals identical apart from that one real dependency must not produce the same span.
    /// </summary>
    [Fact]
    public async Task A_real_cross_agent_dependency_lengthens_the_span()
    {
        await using var independent = new DuckDbStore(":memory:");
        await CreateRunAsync(independent);
        await independent.AppendWorkflowEventsAsync(
            "project-a", "run-1", "observer-1", FanOutJournal(messageBetweenWorkers: false),
            [], TestContext.Current.CancellationToken);
        var independentSpan = (await ReadGraphAsync(independent)).Statistics.TInfinityMs;

        await using var dependent = new DuckDbStore(":memory:");
        await CreateRunAsync(dependent);
        await dependent.AppendWorkflowEventsAsync(
            "project-a", "run-1", "observer-1", FanOutJournal(messageBetweenWorkers: true),
            [], TestContext.Current.CancellationToken);
        var dependentSpan = (await ReadGraphAsync(dependent)).Statistics.TInfinityMs;

        Assert.True(
            dependentSpan > independentSpan,
            $"chaining the workers through a real data dependency left the span unchanged " +
            $"({dependentSpan}ms vs {independentSpan}ms) — Data edges are not being traversed");
    }

    /// <summary>
    /// Each agent stamps its own clock. A skewed journal reporting an end before its start must
    /// not drive the concurrency sweep below zero or produce negative durations.
    /// </summary>
    [Fact]
    public async Task Clock_skew_cannot_produce_negative_concurrency_or_duration()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);

        await store.AppendWorkflowEventsAsync(
            "project-a", "run-1", "observer-1",
            [
                Event("attempt", 1, WorkflowJournalEventKind.AttemptStarted, attemptId: "attempt-1"),
                Event("skew-spawn", 2, WorkflowJournalEventKind.AgentSpawned, 500, "attempt-1", "skewed"),
                Event("skew-start", 3, WorkflowJournalEventKind.AgentStarted, 500, "attempt-1", "skewed"),
                // Completed 400ms BEFORE it started — the other machine's clock is behind.
                Event("skew-end", 4, WorkflowJournalEventKind.AgentCompleted, 100, "attempt-1", "skewed"),
                Event("attempt-end", 5, WorkflowJournalEventKind.AttemptCompleted, 600, "attempt-1")
            ],
            [], TestContext.Current.CancellationToken);

        var statistics = (await ReadGraphAsync(store)).Statistics;

        Assert.True(statistics.PeakConcurrency >= 0, "inverted interval drove the sweep negative");
        Assert.True(statistics.T1Ms >= 0, "inverted interval produced negative work");
        Assert.True(statistics.TInfinityMs >= 0, "inverted interval produced a negative span");
        Assert.True(statistics.TInfinityMs <= statistics.T1Ms);
    }

    /// <summary>
    /// The snapshot contract promises "a deterministic graph projection at one journal cursor".
    /// The critical-path endpoint was chosen with MaxBy over a Dictionary, so equal-score paths
    /// resolved on enumeration order rather than on anything in the journal.
    /// </summary>
    [Fact]
    public async Task Repeated_reads_of_one_cursor_return_an_identical_critical_path()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);
        await store.AppendWorkflowEventsAsync(
            "project-a", "run-1", "observer-1", FanOutJournal(messageBetweenWorkers: false),
            [], TestContext.Current.CancellationToken);

        var first = (await ReadGraphAsync(store)).Statistics;
        var second = (await ReadGraphAsync(store)).Statistics;
        var third = (await ReadGraphAsync(store)).Statistics;

        Assert.Equal(first.CriticalPathNodeIds, second.CriticalPathNodeIds);
        Assert.Equal(second.CriticalPathNodeIds, third.CriticalPathNodeIds);
        Assert.Equal(first.T1Ms, third.T1Ms);
        Assert.Equal(first.TInfinityMs, third.TInfinityMs);
    }

    /// <summary>
    /// "A deterministic graph projection at one journal cursor" means the projection is a
    /// function of the journal and nothing else. The active attempt used to be seeded from
    /// run.ActiveAttemptId — a column the append path rewrites — so the projection depended on
    /// mutable state beside the journal rather than on the journal itself. Rebuilding from the
    /// same events must reproduce the same graph exactly, including across an attempt that
    /// completed and a later one that resumed.
    /// </summary>
    [Fact]
    public async Task Rebuilding_from_the_same_journal_reproduces_the_same_graph()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);

        await store.AppendWorkflowEventsAsync(
            "project-a", "run-1", "observer-1",
            [
                Event("a1", 1, WorkflowJournalEventKind.AttemptStarted, 0, "attempt-1"),
                Event("a1-w", 2, WorkflowJournalEventKind.AgentStarted, 0, "attempt-1", "worker"),
                Event("a1-x", 3, WorkflowJournalEventKind.AgentCompleted, 100, "attempt-1", "worker"),
                Event("a1-end", 4, WorkflowJournalEventKind.AttemptCompleted, 110, "attempt-1"),
                Event("a2", 5, WorkflowJournalEventKind.AttemptStarted, 120, "attempt-2"),
                Event("a2-w", 6, WorkflowJournalEventKind.AgentStarted, 120, "attempt-2", "worker"),
                Event("a2-x", 7, WorkflowJournalEventKind.AgentCompleted, 400, "attempt-2", "worker"),
                Event("a2-end", 8, WorkflowJournalEventKind.AttemptCompleted, 410, "attempt-2")
            ],
            [], TestContext.Current.CancellationToken);

        var before = await ReadGraphAsync(store);
        await store.RebuildWorkflowProjectionAsync("project-a", "run-1", TestContext.Current.CancellationToken);
        var after = await ReadGraphAsync(store);

        Assert.Equal(
            before.Nodes.Select(static node => node.NodeId),
            after.Nodes.Select(static node => node.NodeId));
        Assert.Equal(
            before.Edges.Select(static edge => edge.EdgeId),
            after.Edges.Select(static edge => edge.EdgeId));
        Assert.Equal(before.Statistics.T1Ms, after.Statistics.T1Ms);
        Assert.Equal(before.Statistics.TInfinityMs, after.Statistics.TInfinityMs);
        Assert.Equal(before.Statistics.CriticalPathNodeIds, after.Statistics.CriticalPathNodeIds);
        Assert.Equal(before.Statistics.WorkerCount, after.Statistics.WorkerCount);
        Assert.Equal(before.JournalSequence, after.JournalSequence);

        // Both attempts survive the rebuild: the journal established them, not the run row.
        Assert.Contains(after.Nodes, static node => node.NodeId == "attempt:attempt-1");
        Assert.Contains(after.Nodes, static node => node.NodeId == "attempt:attempt-2");
    }

    [Fact]
    public async Task Agent_work_is_fragmented_by_the_union_of_only_precisely_owned_children()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);

        await store.AppendWorkflowEventsAsync(
            "project-a", "run-1", "observer-1",
            [
                Event("attempt", 1, WorkflowJournalEventKind.AttemptStarted, attemptId: "attempt-1"),
                Event("root-start", 2, WorkflowJournalEventKind.AgentStarted, 0, "attempt-1", "root"),
                Event("child-start", 3, WorkflowJournalEventKind.AgentStarted, 100, "attempt-1", "child",
                    parentAgentId: "root"),
                Event("tool-start", 4, WorkflowJournalEventKind.ToolStarted, 200, "attempt-1", "root",
                    toolCallId: "tool-1"),
                Event("child-end", 5, WorkflowJournalEventKind.AgentCompleted, 400, "attempt-1", "child",
                    parentAgentId: "root"),
                Event("tool-end", 6, WorkflowJournalEventKind.ToolCompleted, 500, "attempt-1", "root",
                    toolCallId: "tool-1"),
                Event("wait-start", 7, WorkflowJournalEventKind.WaitStarted, 600, "attempt-1", "root",
                    receiverAgentId: "child", data: """{"wait_id":"wait-1"}"""),
                Event("gate-start", 8, WorkflowJournalEventKind.ApprovalRequested, 750, "attempt-1", "root",
                    data: """{"approval_id":"approval-1"}"""),
                Event("wait-end", 9, WorkflowJournalEventKind.WaitCompleted, 800, "attempt-1", "root",
                    receiverAgentId: "child", data: """{"wait_id":"wait-1"}"""),
                Event("gate-end", 10, WorkflowJournalEventKind.ApprovalResolved, 900, "attempt-1", "root",
                    data: """{"approval_id":"approval-1"}"""),
                Event("item-start", 11, WorkflowJournalEventKind.ItemStarted, 950, "attempt-1", "root",
                    data: """{"item_id":"item-1"}"""),
                Event("root-end", 12, WorkflowJournalEventKind.AgentCompleted, 1000, "attempt-1", "root"),
                Event("root-tool-start", 13, WorkflowJournalEventKind.ToolStarted, 1050, "attempt-1",
                    toolCallId: "root-tool"),
                Event("root-tool-end", 14, WorkflowJournalEventKind.ToolCompleted, 1150, "attempt-1",
                    toolCallId: "root-tool"),
                Event("item-end", 15, WorkflowJournalEventKind.ItemCompleted, 1200, "attempt-1", "root",
                    data: """{"item_id":"item-1"}"""),
                Event("attempt-end", 16, WorkflowJournalEventKind.AttemptCompleted, 1200, "attempt-1")
            ],
            [], TestContext.Current.CancellationToken);

        var statistics = (await ReadGraphAsync(store)).Statistics;

        Assert.Equal(1550, statistics.T1Ms);
        Assert.Equal(2, statistics.PeakConcurrency);
        Assert.Equal(2, statistics.WorkerCount);
        Assert.True(statistics.TInfinityMs <= statistics.T1Ms);
    }

    [Fact]
    public async Task Root_tool_without_an_agent_contributes_work_and_peak_concurrency()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);
        await store.AppendWorkflowEventsAsync(
            "project-a",
            "run-1",
            "observer-1",
            [
                Event("root-tool-start", 1, WorkflowJournalEventKind.ToolStarted, 10,
                    toolCallId: "root-tool"),
                Event("root-tool-end", 2, WorkflowJournalEventKind.ToolCompleted, 110,
                    toolCallId: "root-tool")
            ],
            [],
            TestContext.Current.CancellationToken);

        var graph = await ReadGraphAsync(store);

        Assert.Contains(graph.Nodes, static node => node.NodeId == "tool:run:root-tool");
        Assert.Equal(100, graph.Statistics.T1Ms);
        Assert.Equal(100, graph.Statistics.TInfinityMs);
        Assert.Equal(1, graph.Statistics.PeakConcurrency);
        Assert.Equal(1, graph.Statistics.WorkerCount);
    }

    private static readonly string[] s_causalCycleMembers =
        new[]
            {
                "agent:attempt:attempt-1:a",
                "agent:attempt:attempt-1:b",
                "message:a-to-b",
                "message:b-to-a"
            }
            .Order(StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public async Task Causal_cycles_are_condensed_charged_once_and_expanded_in_ordinal_order()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);
        await store.AppendWorkflowEventsAsync(
            "project-a", "run-1", "observer-1",
            [
                Event("attempt", 1, WorkflowJournalEventKind.AttemptStarted, attemptId: "attempt-1"),
                Event("a-start", 2, WorkflowJournalEventKind.AgentStarted, 0, "attempt-1", "a"),
                Event("b-start", 3, WorkflowJournalEventKind.AgentStarted, 0, "attempt-1", "b"),
                Event("a-to-b", 4, WorkflowJournalEventKind.MessageSent, 30, "attempt-1", "a",
                    receiverAgentId: "b"),
                Event("b-to-a", 5, WorkflowJournalEventKind.MessageSent, 40, "attempt-1", "b",
                    receiverAgentId: "a"),
                Event("tool-start", 6, WorkflowJournalEventKind.ToolStarted, 60, "attempt-1", "b",
                    toolCallId: "downstream"),
                Event("tool-end", 7, WorkflowJournalEventKind.ToolCompleted, 100, "attempt-1", "b",
                    toolCallId: "downstream"),
                Event("a-end", 8, WorkflowJournalEventKind.AgentCompleted, 100, "attempt-1", "a"),
                Event("b-end", 9, WorkflowJournalEventKind.AgentCompleted, 100, "attempt-1", "b"),
                Event("attempt-end", 10, WorkflowJournalEventKind.AttemptCompleted, 100, "attempt-1")
            ],
            [], TestContext.Current.CancellationToken);

        var graph = await ReadGraphAsync(store);
        var cycleMembers = s_causalCycleMembers;
        var expectedPath = new[]
        {
            "attempt:attempt-1",
            cycleMembers[0],
            cycleMembers[1],
            cycleMembers[2],
            cycleMembers[3],
            "tool:attempt:attempt-1:downstream"
        };

        Assert.Equal(200, graph.Statistics.T1Ms);
        Assert.Equal(200, graph.Statistics.TInfinityMs);
        Assert.Equal(expectedPath, graph.Statistics.CriticalPathNodeIds);
        Assert.All(
            expectedPath,
            nodeId => Assert.Equal(1, graph.Statistics.CriticalPathNodeIds.Count(item => item == nodeId)));
        Assert.Equal(4, graph.Edges.Count(static edge => edge.Kind is WorkflowEdgeKind.Data));
        Assert.Contains(
            graph.Edges,
            static edge => edge.Kind is WorkflowEdgeKind.Control &&
                           edge.SourceNodeId == "agent:attempt:attempt-1:b" &&
                           edge.TargetNodeId == "tool:attempt:attempt-1:downstream");

        await using var skewedStore = new DuckDbStore(":memory:");
        await CreateRunAsync(skewedStore);
        await skewedStore.AppendWorkflowEventsAsync(
            "project-a", "run-1", "observer-1",
            [
                Event("attempt", 1, WorkflowJournalEventKind.AttemptStarted, 0, "attempt-1"),
                Event("b-start", 2, WorkflowJournalEventKind.AgentStarted, 100, "attempt-1", "b"),
                Event("tool-start", 3, WorkflowJournalEventKind.ToolStarted, 160, "attempt-1", "b",
                    toolCallId: "downstream"),
                Event("a-start", 4, WorkflowJournalEventKind.AgentStarted, 500, "attempt-1", "a"),
                Event("b-to-a", 5, WorkflowJournalEventKind.MessageSent, 110, "attempt-1", "b",
                    receiverAgentId: "a"),
                Event("a-to-b", 6, WorkflowJournalEventKind.MessageSent, 590, "attempt-1", "a",
                    receiverAgentId: "b"),
                Event("tool-end", 7, WorkflowJournalEventKind.ToolCompleted, 200, "attempt-1", "b",
                    toolCallId: "downstream"),
                Event("b-end", 8, WorkflowJournalEventKind.AgentCompleted, 200, "attempt-1", "b"),
                Event("a-end", 9, WorkflowJournalEventKind.AgentCompleted, 600, "attempt-1", "a"),
                Event("attempt-end", 10, WorkflowJournalEventKind.AttemptCompleted, 600, "attempt-1")
            ],
            [], TestContext.Current.CancellationToken);
        var skewed = await ReadGraphAsync(skewedStore);

        Assert.Equal(graph.Statistics.T1Ms, skewed.Statistics.T1Ms);
        Assert.Equal(graph.Statistics.TInfinityMs, skewed.Statistics.TInfinityMs);
        Assert.Equal(expectedPath, skewed.Statistics.CriticalPathNodeIds);
        Assert.Equal(
            graph.Edges
                .Where(static edge => edge.Kind is
                    WorkflowEdgeKind.Data or WorkflowEdgeKind.Control or WorkflowEdgeKind.Gate)
                .Select(static edge => (edge.SourceNodeId, edge.TargetNodeId, edge.Kind))
                .OrderBy(static edge => edge.SourceNodeId, StringComparer.Ordinal)
                .ThenBy(static edge => edge.TargetNodeId, StringComparer.Ordinal)
                .ThenBy(static edge => edge.Kind),
            skewed.Edges
                .Where(static edge => edge.Kind is
                    WorkflowEdgeKind.Data or WorkflowEdgeKind.Control or WorkflowEdgeKind.Gate)
                .Select(static edge => (edge.SourceNodeId, edge.TargetNodeId, edge.Kind))
                .OrderBy(static edge => edge.SourceNodeId, StringComparer.Ordinal)
                .ThenBy(static edge => edge.TargetNodeId, StringComparer.Ordinal)
                .ThenBy(static edge => edge.Kind));
    }

    [Fact]
    public async Task Generated_identifiers_are_unambiguous_bounded_and_rebuild_stable()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);
        var maximumAttempt = new string('a', 128);
        var maximumAgent = new string('g', 128);
        var maximumRun = new string('r', 128);
        var maximumTool = new string('t', 128);
        var maximumEvent = new string('e', 160);
        var exactAttempt = new string('x', 49);
        var overAttempt = new string('x', 50);
        var sharedPrefix = new string('p', 127);
        var overAgentA = sharedPrefix + "a";
        var overAgentB = sharedPrefix + "b";
        var nonBmpAgent = string.Concat(Enumerable.Repeat("😀", 128));

        await store.CreateWorkflowRunAsync(
            new WorkflowRunStorageRow(
                "project-a",
                maximumRun,
                null,
                null,
                WorkflowRunStatus.Active,
                s_startedAt,
                null,
                0,
                null,
                null),
            TestContext.Current.CancellationToken);
        var maximumRunGraph = await store.GetWorkflowGraphAsync(
            "project-a",
            maximumRun,
            null,
            10,
            null,
            10,
            TestContext.Current.CancellationToken);
        Assert.Contains(maximumRunGraph!.Nodes, node => node.NodeId == $"run:{maximumRun}");

        await store.AppendWorkflowEventsAsync(
            "project-a", "run-1", "observer-1",
            [
                Event("maximum-tool", 1, WorkflowJournalEventKind.ToolStarted,
                    toolCallId: maximumTool),
                Event(maximumEvent, 2, WorkflowJournalEventKind.MessageSent),
                Event("run-scope", 3, WorkflowJournalEventKind.AgentStarted, agentId: "worker"),
                Event("delimiter", 4, WorkflowJournalEventKind.AgentStarted, agentId: "a:b"),
                Event("backslash", 5, WorkflowJournalEventKind.AgentStarted, agentId: @"a\cb"),
                Event("non-bmp", 6, WorkflowJournalEventKind.AgentStarted, agentId: nonBmpAgent),
                Event("maximum-attempt", 7, WorkflowJournalEventKind.AttemptStarted,
                    attemptId: maximumAttempt),
                Event("maximum-agent", 8, WorkflowJournalEventKind.AgentStarted,
                    attemptId: maximumAttempt, agentId: maximumAgent),
                Event("literal-run", 9, WorkflowJournalEventKind.AgentStarted,
                    attemptId: "run", agentId: "worker"),
                Event("exact-parent", 10, WorkflowJournalEventKind.AgentStarted,
                    attemptId: exactAttempt, agentId: "parent"),
                Event("exact-composite", 11, WorkflowJournalEventKind.AgentStarted,
                    attemptId: exactAttempt, agentId: maximumAgent, parentAgentId: "parent"),
                Event("over-a", 12, WorkflowJournalEventKind.AgentStarted,
                    attemptId: overAttempt, agentId: overAgentA),
                Event("over-b", 13, WorkflowJournalEventKind.AgentStarted,
                    attemptId: overAttempt, agentId: overAgentB)
            ],
            [], TestContext.Current.CancellationToken);

        var graph = await ReadGraphAsync(store);
        var ids = graph.Nodes.Select(static node => node.NodeId).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("agent:run:worker", ids);
        Assert.Contains("agent:attempt:run:worker", ids);
        Assert.Contains(@"agent:run:a\cb", ids);
        Assert.Contains(@"agent:run:a\\cb", ids);
        Assert.Contains($"attempt:{maximumAttempt}", ids);
        Assert.Contains($"tool:run:{maximumTool}", ids);
        Assert.Contains($"message:{maximumEvent}", ids);
        Assert.Contains($"agent:run:{nonBmpAgent}", ids);
        Assert.Contains(
            ExpectedHashedId("agent", "attempt", maximumAttempt, maximumAgent),
            ids);
        var exactNodeId = $"agent:attempt:{exactAttempt}:{maximumAgent}";
        Assert.Equal(192, exactNodeId.EnumerateRunes().Count());
        Assert.Contains(exactNodeId, ids);
        var overNodeIdA = ExpectedHashedId("agent", "attempt", overAttempt, overAgentA);
        var overNodeIdB = ExpectedHashedId("agent", "attempt", overAttempt, overAgentB);
        Assert.Equal(193, $"agent:attempt:{overAttempt}:{overAgentA}".EnumerateRunes().Count());
        Assert.Contains(overNodeIdA, ids);
        Assert.Contains(overNodeIdB, ids);
        Assert.NotEqual(overNodeIdA, overNodeIdB);
        Assert.Matches("^agent~[a-f0-9]{64}$", overNodeIdA);
        var exactParentId = $"agent:attempt:{exactAttempt}:parent";
        var boundedEdge = Assert.Single(
            graph.Edges,
            edge => edge.SourceNodeId == exactParentId && edge.TargetNodeId == exactNodeId);
        Assert.Equal(
            ExpectedHashedId("control", exactParentId, exactNodeId),
            boundedEdge.EdgeId);
        Assert.Matches("^control~[a-f0-9]{64}$", boundedEdge.EdgeId);
        Assert.Equal(ids.Count, graph.Nodes.Count);
        Assert.All(graph.Nodes, static node =>
            Assert.InRange(node.NodeId.EnumerateRunes().Count(), 1, 192));
        Assert.All(graph.Edges, static edge =>
            Assert.InRange(edge.EdgeId.EnumerateRunes().Count(), 1, 192));
        var page = await store.GetWorkflowGraphAsync(
            "project-a", "run-1", null, 1, null, 1, TestContext.Current.CancellationToken);
        Assert.NotNull(page!.NextNodeCursor);
        Assert.NotNull(page.NextEdgeCursor);
        Assert.Equal(page.Nodes[^1].NodeId, page.NextNodeCursor);
        Assert.Equal(page.Edges[^1].EdgeId, page.NextEdgeCursor);
        Assert.InRange(page.NextNodeCursor!.EnumerateRunes().Count(), 1, 192);
        Assert.InRange(page.NextEdgeCursor!.EnumerateRunes().Count(), 1, 192);

        await store.RebuildWorkflowProjectionAsync(
            "project-a", "run-1", TestContext.Current.CancellationToken);
        var rebuilt = await ReadGraphAsync(store);
        Assert.Equal(
            graph.Nodes.Select(static node => node.NodeId),
            rebuilt.Nodes.Select(static node => node.NodeId));
        Assert.Equal(
            graph.Edges.Select(static edge => edge.EdgeId),
            rebuilt.Edges.Select(static edge => edge.EdgeId));
    }

    [Fact]
    public async Task Gate_and_item_identifiers_preserve_attempt_and_domain_scope()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);
        await store.AppendWorkflowEventsAsync(
            "project-a", "run-1", "observer-1",
            [
                Event("run-approval", 1, WorkflowJournalEventKind.ApprovalRequested,
                    data: """{"approval_id":"shared"}"""),
                Event("run-command", 2, WorkflowJournalEventKind.ControlRequested,
                    data: """{"command_id":"shared"}"""),
                Event("run-item", 3, WorkflowJournalEventKind.ItemStarted,
                    data: """{"item_id":"shared"}"""),
                Event("a1", 4, WorkflowJournalEventKind.AttemptStarted, attemptId: "attempt-1"),
                Event("a1-approval", 5, WorkflowJournalEventKind.ApprovalRequested,
                    attemptId: "attempt-1", data: """{"approval_id":"shared"}"""),
                Event("a1-command", 6, WorkflowJournalEventKind.ControlRequested,
                    attemptId: "attempt-1", data: """{"command_id":"shared"}"""),
                Event("a1-item", 7, WorkflowJournalEventKind.ItemStarted,
                    attemptId: "attempt-1", data: """{"item_id":"shared"}"""),
                Event("a2", 8, WorkflowJournalEventKind.AttemptStarted, attemptId: "attempt-2"),
                Event("a2-approval", 9, WorkflowJournalEventKind.ApprovalRequested,
                    attemptId: "attempt-2", data: """{"approval_id":"shared"}"""),
                Event("a2-command", 10, WorkflowJournalEventKind.ControlRequested,
                    attemptId: "attempt-2", data: """{"command_id":"shared"}"""),
                Event("a2-item", 11, WorkflowJournalEventKind.ItemStarted,
                    attemptId: "attempt-2", data: """{"item_id":"shared"}""")
            ],
            [],
            TestContext.Current.CancellationToken);

        var ids = (await ReadGraphAsync(store)).Nodes
            .Select(static node => node.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("gate:run:approval:shared", ids);
        Assert.Contains("gate:run:command:shared", ids);
        Assert.Contains("item:run:shared", ids);
        Assert.Contains("gate:attempt:attempt-1:approval:shared", ids);
        Assert.Contains("gate:attempt:attempt-1:command:shared", ids);
        Assert.Contains("item:attempt:attempt-1:shared", ids);
        Assert.Contains("gate:attempt:attempt-2:approval:shared", ids);
        Assert.Contains("gate:attempt:attempt-2:command:shared", ids);
        Assert.Contains("item:attempt:attempt-2:shared", ids);
    }

    [Fact]
    public async Task Paging_skips_nothing_when_the_projection_grows_between_pages()
    {
        await using var store = new DuckDbStore(":memory:");
        await CreateRunAsync(store);

        var events = new List<WorkflowEventWrite>
        {
            Event("attempt", 1, WorkflowJournalEventKind.AttemptStarted, attemptId: "attempt-1")
        };
        ulong sequence = 2;
        for (var index = 0; index < 40; index++)
        {
            events.Add(AgentEvent($"start-{index:D3}", sequence++, WorkflowJournalEventKind.AgentStarted, $"worker-{index:D3}"));
            events.Add(AgentEvent($"end-{index:D3}", sequence++, WorkflowJournalEventKind.AgentCompleted, $"worker-{index:D3}"));
        }

        await store.AppendWorkflowEventsAsync(
            "project-a", "run-1", "observer-1", events, [], TestContext.Current.CancellationToken);

        var original = (await ReadPageAsync(store, null, 1000)).Nodes
            .Select(static node => node.NodeId)
            .ToHashSet(StringComparer.Ordinal);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        for (var page = 0; page < 20; page++)
        {
            var snapshot = await ReadPageAsync(store, cursor, 10);
            foreach (var node in snapshot.Nodes)
            {
                Assert.True(seen.Add(node.NodeId), $"node '{node.NodeId}' was returned on two pages");
            }

            if (!snapshot.HasMoreNodes)
                break;
            cursor = snapshot.NextNodeCursor;
            Assert.NotNull(cursor);

            await store.AppendWorkflowEventsAsync(
                "project-a", "run-1", "observer-1",
                [AgentEvent($"insert-{page:D2}", sequence++, WorkflowJournalEventKind.AgentStarted, $"aaa-{page:D2}")],
                [], TestContext.Current.CancellationToken);
        }

        var missed = original.Except(seen, StringComparer.Ordinal).ToArray();
        Assert.True(
            missed.Length is 0,
            $"paging skipped {missed.Length} node(s) that existed before the first page: " +
            string.Join(", ", missed.Take(5)));
    }

    private static async Task<WorkflowGraphSnapshot> ReadPageAsync(DuckDbStore store, string? cursor, int limit)
    {
        var snapshot = await store.GetWorkflowGraphAsync(
            "project-a", "run-1", cursor, limit, null, 2000, TestContext.Current.CancellationToken);
        Assert.NotNull(snapshot);
        return snapshot;
    }

    private static WorkflowEventWrite AgentEvent(
        string eventId,
        ulong sourceSequence,
        WorkflowJournalEventKind kind,
        string agentId) =>
        Event(eventId, sourceSequence, kind, attemptId: "attempt-1", agentId: agentId);

    /// <summary>
    /// Two workers fanned out from one root. With <paramref name="messageBetweenWorkers"/> the
    /// second worker consumes the first's output, which is a genuine dependency; without it the
    /// two are independent and only share an ancestor.
    /// </summary>
    private static List<WorkflowEventWrite> FanOutJournal(bool messageBetweenWorkers)
    {
        var events = new List<WorkflowEventWrite>
        {
            Event("attempt", 1, WorkflowJournalEventKind.AttemptStarted, attemptId: "attempt-1"),
            Event("root-spawn", 2, WorkflowJournalEventKind.AgentSpawned, 0, "attempt-1", "root"),
            Event("root-start", 3, WorkflowJournalEventKind.AgentStarted, 0, "attempt-1", "root"),
            Event("w1-spawn", 4, WorkflowJournalEventKind.AgentSpawned, 10, "attempt-1", "worker-1",
                parentAgentId: "root"),
            Event("w1-start", 5, WorkflowJournalEventKind.AgentStarted, 10, "attempt-1", "worker-1",
                parentAgentId: "root"),
            Event("w2-spawn", 6, WorkflowJournalEventKind.AgentSpawned, 10, "attempt-1", "worker-2",
                parentAgentId: "root"),
            Event("w2-start", 7, WorkflowJournalEventKind.AgentStarted, 10, "attempt-1", "worker-2",
                parentAgentId: "root"),
        };

        if (messageBetweenWorkers)
        {
            events.Add(Event("handoff", 8, WorkflowJournalEventKind.MessageSent, 300, "attempt-1",
                "worker-1", receiverAgentId: "worker-2"));
        }

        events.Add(Event("w1-end", 9, WorkflowJournalEventKind.AgentCompleted, 400, "attempt-1", "worker-1"));
        events.Add(Event("w2-end", 10, WorkflowJournalEventKind.AgentCompleted, 800, "attempt-1", "worker-2"));
        events.Add(Event("root-end", 11, WorkflowJournalEventKind.AgentCompleted, 810, "attempt-1", "root"));
        events.Add(Event("attempt-end", 12, WorkflowJournalEventKind.AttemptCompleted, 810, "attempt-1"));
        return events;
    }

    private static async Task<WorkflowGraphSnapshot> ReadGraphAsync(DuckDbStore store)
    {
        var graph = await store.GetWorkflowGraphAsync(
            "project-a", "run-1", null, 1000, null, 2000, TestContext.Current.CancellationToken);
        Assert.NotNull(graph);
        return graph;
    }

    private static string ExpectedHashedId(string kind, params string[] parts)
    {
        var canonical = new StringBuilder();
        foreach (var part in new[] { kind }.Concat(parts))
        {
            canonical.Append(Encoding.UTF8.GetByteCount(part));
            canonical.Append('#');
            canonical.Append(part);
        }
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return $"{kind}~{Convert.ToHexStringLower(digest)}";
    }

    private static Task<WorkflowRunStorageRow> CreateRunAsync(DuckDbStore store) =>
        store.CreateWorkflowRunAsync(
            new WorkflowRunStorageRow(
                "project-a",
                "run-1",
                "thread-1",
                "Graph invariant fixture",
                WorkflowRunStatus.Active,
                s_startedAt,
                null,
                0,
                null,
                null),
            TestContext.Current.CancellationToken);

    private static WorkflowEventWrite Event(
        string eventId,
        ulong sourceSequence,
        WorkflowJournalEventKind kind,
        int milliseconds = 0,
        string? attemptId = null,
        string? agentId = null,
        string? parentAgentId = null,
        string? receiverAgentId = null,
        string? toolCallId = null,
        string? data = null) =>
        new(
            eventId,
            sourceSequence,
            s_startedAt.AddMilliseconds(milliseconds),
            kind,
            "thread-1",
            null,
            attemptId,
            agentId,
            parentAgentId,
            receiverAgentId,
            toolCallId,
            [],
            data);
}
