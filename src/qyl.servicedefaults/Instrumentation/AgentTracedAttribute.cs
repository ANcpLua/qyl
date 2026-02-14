namespace Qyl.ServiceDefaults.Instrumentation;

/// <summary>
///     Marks a method for automatic agent invocation tracing with OTel spans.
/// </summary>
/// <remarks>
///     When applied, the source generator intercepts calls to the decorated method
///     and wraps them with a <c>gen_ai.agent.invoke</c> span using the "qyl.agent" ActivitySource.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AgentTracedAttribute : Attribute
{
    /// <summary>
    ///     Gets or sets the agent name to record as <c>gen_ai.agent.name</c>.
    ///     If not specified, defaults to the method name.
    /// </summary>
    public string? AgentName { get; set; }
}
