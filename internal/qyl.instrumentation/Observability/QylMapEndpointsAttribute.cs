namespace Qyl.Instrumentation;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class QylMapEndpointsAttribute(int order = 100) : Attribute
{
    public int Order { get; } = order;
}
