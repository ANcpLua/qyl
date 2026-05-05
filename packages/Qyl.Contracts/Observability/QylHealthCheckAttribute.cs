namespace Qyl.Contracts.Observability;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class QylHealthCheckAttribute(string Name, params string[] Tags) : Attribute
{
    public string Name { get; } = Name;
    public string[] Tags { get; } = Tags;
}
