namespace Qyl.Instrumentation.Instrumentation;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class TagAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;
}
