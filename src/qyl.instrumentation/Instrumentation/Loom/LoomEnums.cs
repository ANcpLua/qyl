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

public enum ToolSideEffect
{
    None,
    ReadsExternalState,
    WritesExternalState,
    MutatesCode,
    Deploys,
    ClosesIssue
}
