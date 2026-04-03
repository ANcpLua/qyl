namespace Qyl.Instrumentation.Instrumentation.Loom;

[AttributeUsage(AttributeTargets.Method)]
public sealed class LoomToolAttribute(string name) : Attribute
{
    public string Name { get; } = name;
    public string Description { get; init; } = string.Empty;
    public LoomPhase Phase { get; init; }
    public string? UseOnlyWhen { get; init; }
    public string? DoNotUseWhen { get; init; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class LoomContractAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class LoomStepAttribute(string id) : Attribute
{
    public string Id { get; } = id;
    public LoomPhase Phase { get; init; }
    public string? Description { get; init; }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class LoomWorkflowAttribute(string id, Type runStateType, params string[] stepIds) : Attribute
{
    public string Id { get; } = id;
    public Type RunStateType { get; } = runStateType;
    public IReadOnlyList<string> StepIds { get; } = stepIds;
    public string? Description { get; init; }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresCapabilityAttribute(string capability) : Attribute
{
    public string Capability { get; } = capability;
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequiresApprovalAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ToolSideEffectAttribute(ToolSideEffect sideEffect) : Attribute
{
    public ToolSideEffect SideEffect { get; } = sideEffect;
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class EmitsStructuredOutputAttribute(Type outputType) : Attribute
{
    public Type OutputType { get; } = outputType;
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class LoomBudgetAttribute : Attribute
{
    public int MaxAttempts { get; init; } = 1;
    public int MaxToolCalls { get; init; } = 8;
    public int MaxTokens { get; init; } = 16000;
}
