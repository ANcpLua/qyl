// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Autofix.Workflow;

internal sealed class AutofixReportAssemblyState
{
    private readonly ConcurrentDictionary<string, RunSnapshot> _snapshots = new(StringComparer.Ordinal);

    public void Record(string runId, FixabilityVerdict verdict) =>
        _snapshots.AddOrUpdate(runId,
            _ => new RunSnapshot { Fixability = verdict },
            (_, prev) => prev with { Fixability = verdict });

    public void Record(string runId, ContextSummary context) =>
        _snapshots.AddOrUpdate(runId,
            _ => new RunSnapshot { Context = context },
            (_, prev) => prev with { Context = context });

    public void Record(string runId, HypothesisVerdict hypothesis) =>
        _snapshots.AddOrUpdate(runId,
            _ => new RunSnapshot { Hypothesis = hypothesis },
            (_, prev) => prev with { Hypothesis = hypothesis });

    public void Record(string runId, SolutionDraft solution) =>
        _snapshots.AddOrUpdate(runId,
            _ => new RunSnapshot { Solution = solution },
            (_, prev) => prev with { Solution = solution });

    public void Record(string runId, ConfidenceAudit audit) =>
        _snapshots.AddOrUpdate(runId,
            _ => new RunSnapshot { Audit = audit },
            (_, prev) => prev with { Audit = audit });

    public RunSnapshot Snapshot(string runId) =>
        _snapshots.TryGetValue(runId, out var snap) ? snap : new RunSnapshot();

    public bool TryRemove(string runId) => _snapshots.TryRemove(runId, out _);

    public sealed record RunSnapshot
    {
        public FixabilityVerdict? Fixability { get; init; }
        public ContextSummary? Context { get; init; }
        public HypothesisVerdict? Hypothesis { get; init; }
        public SolutionDraft? Solution { get; init; }
        public ConfidenceAudit? Audit { get; init; }
    }
}
