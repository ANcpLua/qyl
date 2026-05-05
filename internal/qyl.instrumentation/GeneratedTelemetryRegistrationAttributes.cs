namespace Qyl.Instrumentation;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedActivitySourceAttribute(string name) : Attribute
{
    public string Name { get; } = Guard.NotNull(name);
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedMeterAttribute(string name) : Attribute
{
    public string Name { get; } = Guard.NotNull(name);
}
