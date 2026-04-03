namespace Qyl.Instrumentation.Instrumentation.Loom;

public sealed record LoomParameterBindingDescriptor(
    string Name,
    Type Type,
    bool IsNullable,
    bool HasDefaultValue,
    string? DefaultValueLiteral,
    string? Description,
    bool IsSchemaVisible,
    bool IsInfrastructureBound,
    IReadOnlyList<string> EnumValues);

public sealed record LoomResultDescriptor(
    Type? OutputType,
    Type? StructuredOutputType,
    string? ResultSchemaHint,
    bool HasStructuredOutput,
    bool IsSchemaVisible);

public sealed record LoomTelemetryDescriptor(
    string Name,
    Type DeclaringType,
    string MethodName,
    LoomPhase Phase,
    bool IsAwaitable,
    bool ReturnsValue,
    ToolSideEffect SideEffect,
    IReadOnlyList<string> RequiredCapabilities);

public sealed record LoomPolicyDescriptor(
    string Name,
    Type DeclaringType,
    string MethodName,
    LoomPhase Phase,
    bool RequiresApproval,
    ToolSideEffect SideEffect,
    int MaxAttempts,
    int MaxToolCalls,
    int MaxTokens,
    IReadOnlyList<string> RequiredCapabilities);

public sealed record LoomRuntimeMetadataDescriptor(
    string Name,
    Type DeclaringType,
    string MethodName,
    LoomPhase Phase,
    IReadOnlyList<LoomParameterBindingDescriptor> ParameterBindings,
    LoomResultDescriptor Result,
    LoomTelemetryDescriptor Telemetry,
    LoomPolicyDescriptor Policy);

public sealed record LoomManifestEntry(
    string Kind,
    string Name,
    Type SymbolType,
    string? MemberName,
    LoomPhase? Phase,
    string? Description);
