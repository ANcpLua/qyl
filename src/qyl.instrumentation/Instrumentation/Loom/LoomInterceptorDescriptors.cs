namespace Qyl.Instrumentation.Instrumentation.Loom;

/// <summary>
///     Interceptor metadata for a Loom tool invocation span.
///     Span name follows OTel 1.40 GenAI semantic conventions: <c>execute_tool {ToolName}</c>.
/// </summary>
public sealed record LoomToolInterceptorDescriptor(
    string ToolName,
    string SpanName,
    LoomPhase Phase,
    IReadOnlyList<string> RequiredCapabilities,
    bool RequiresApproval,
    ToolSideEffect SideEffect);

/// <summary>
///     Interceptor metadata for a Loom workflow step execution span.
///     Span name follows OTel 1.40 GenAI semantic conventions: <c>executor.process {StepId}</c>.
/// </summary>
public sealed record LoomStepInterceptorDescriptor(
    string StepId,
    string SpanName,
    LoomPhase Phase,
    Type ExecutorType);

/// <summary>
///     Interceptor metadata for a Loom workflow execution span.
///     Span name follows OTel 1.40 GenAI semantic conventions: <c>workflow.run {WorkflowId}</c>.
/// </summary>
public sealed record LoomWorkflowInterceptorDescriptor(
    string WorkflowId,
    string SpanName,
    IReadOnlyList<string> StepIds);

/// <summary>
///     Aggregated interceptor manifest containing all tool, step, and workflow interceptor descriptors.
///     Generated at compile time by the Loom source generator.
/// </summary>
public sealed record LoomInterceptorManifest(
    IReadOnlyList<LoomToolInterceptorDescriptor> Tools,
    IReadOnlyList<LoomStepInterceptorDescriptor> Steps,
    IReadOnlyList<LoomWorkflowInterceptorDescriptor> Workflows);
