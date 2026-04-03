namespace Qyl.Instrumentation.Instrumentation.Loom;

public sealed record LoomCapabilityDescriptor(
    string Capability,
    IReadOnlyList<string> ToolNames,
    bool AnyRequiresApproval,
    IReadOnlyList<ToolSideEffect> SideEffects);
