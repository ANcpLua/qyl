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
    /// The graph projection is rebuilt from a journal that keeps growing, so pagination has to
    /// survive rows appearing between pages. An offset cursor could not: inserting nodes that
    /// sort before the cursor shifted every later row right, and the reader silently skipped
    /// exactly as many as were inserted while has_more kept reporting normally. A keyset cursor
    /// is anchored to the last id returned, so nothing after it can be shifted past the reader.
    /// </summary>
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

            // Between pages, new nodes land that sort BEFORE the cursor — the exact shift an
            // offset reader cannot survive.
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
        string? receiverAgentId = null) =>
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
            null,
            [],
            null);
}
