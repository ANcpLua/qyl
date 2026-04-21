namespace Qyl.Instrumentation.Instrumentation.Loom;

public delegate ValueTask<object?> LoomToolInvoker(
    IServiceProvider services,
    object?[] args,
    CancellationToken cancellationToken);

public sealed record LoomToolParameterDescriptor(
    string Name,
    Type Type,
    bool IsNullable,
    bool HasDefaultValue,
    string? DefaultValueLiteral,
    string? Description,
    IReadOnlyList<string> EnumValues);

public sealed record LoomToolDescriptor(
    string Name,
    string Description,
    LoomPhase Phase,
    string? UseOnlyWhen,
    string? DoNotUseWhen,
    Type DeclaringType,
    string MethodName,
    Type? OutputType,
    IReadOnlyList<LoomToolParameterDescriptor> Parameters,
    IReadOnlyList<string> RequiredCapabilities,
    bool RequiresApproval,
    ToolSideEffect SideEffect,
    LoomToolInvoker Invoker);

public sealed record LoomContractPropertyDescriptor(
    string Name,
    Type Type,
    bool IsNullable,
    bool IsRequired,
    IReadOnlyList<string> EnumValues);

public sealed record LoomContractDescriptor(
    string Name,
    Type Type,
    IReadOnlyList<LoomContractPropertyDescriptor> Properties);

public sealed record LoomStepDescriptor(
    string Id,
    LoomPhase Phase,
    Type ExecutorType,
    string? Description);

public sealed record LoomWorkflowDescriptor(
    string Id,
    Type RunStateType,
    IReadOnlyList<string> StepIds,
    string? Description);
