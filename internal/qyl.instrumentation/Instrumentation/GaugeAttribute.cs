namespace Qyl.Instrumentation.Instrumentation;

[AttributeUsage(AttributeTargets.Method)]
public sealed class GaugeAttribute(string name) : Attribute
{
    public string Name { get; } = Guard.NotNull(name);

    public string? Unit { get; set; }

    public string? Description { get; set; }
}
