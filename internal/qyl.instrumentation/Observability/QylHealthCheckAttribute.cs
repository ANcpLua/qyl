namespace Qyl.Instrumentation;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class QylHealthCheckAttribute(string name, params string[] tags) : Attribute
{
    public string Name { get; } = name;
    public string[] Tags { get; } = tags;
}
