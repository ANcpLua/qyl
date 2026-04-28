// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Autofix.Workflow.Executors;

/// Trivial passthrough executor — bridges <c>AddSwitch</c> default cases into an
/// <c>AddExternalCall</c> HITL port. The switch needs an executor as target, the
/// port needs an executor as source; this fills both roles without transforming
/// the message. The framework's <c>RequestInfoEvent</c> already carries the gate
/// payload, so no custom lifecycle event is emitted here.
internal sealed class StoppingPointGateExecutor<T>(string id) : Executor<T, T>(id)
    where T : notnull
{
    public override ValueTask<T> HandleAsync(T input, IWorkflowContext _, CancellationToken __ = default) =>
        ValueTask.FromResult(input);
}
