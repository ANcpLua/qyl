namespace Qyl.Instrumentation.Instrumentation;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TracedAttribute(string activitySourceName) : Attribute
{
    public string ActivitySourceName { get; } = Guard.NotNull(activitySourceName);

    public string? SpanName { get; set; }

    public ActivityKind Kind { get; set; } = ActivityKind.Internal;

    public bool RootSpan { get; set; }
}
