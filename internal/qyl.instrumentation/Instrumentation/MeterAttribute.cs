namespace Qyl.Instrumentation.Instrumentation;

[AttributeUsage(AttributeTargets.Class)]
public sealed class MeterAttribute(string name) : Attribute
{
    public string Name { get; } = Guard.NotNull(name);

    public string? Version { get; set; }
}
