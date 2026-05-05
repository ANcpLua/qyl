namespace Qyl.Instrumentation.Instrumentation;

[AttributeUsage(AttributeTargets.ReturnValue)]
public sealed class TracedReturnAttribute(string tagName) : Attribute
{
    public string TagName { get; } = Guard.NotNull(tagName);

    public string? Property { get; set; }
}
