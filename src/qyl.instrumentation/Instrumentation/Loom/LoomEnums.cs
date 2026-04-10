namespace Qyl.Instrumentation.Instrumentation.Loom;

public enum LoomPhase
{
    Detect,
    Plan,
    Fix,
    Verify,
    Report,
    Close
}

public enum SignalType
{
    Errors,
    Latency,
    Throughput,
    Availability,
    CostUsd,
    TokenUsage
}

public enum RunStatus
{
    Pending,
    Running,
    WaitingApproval,
    Completed,
    Failed
}

public enum ToolSideEffect
{
    None,
    ReadsExternalState,
    WritesExternalState,
    MutatesCode,
    Deploys,
    ClosesIssue
}
