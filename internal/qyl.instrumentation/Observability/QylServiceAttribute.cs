namespace Qyl.Instrumentation;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class QylServiceAttribute(QylLifetime lifetime, Type? asInterface = null) : Attribute
{
    public QylLifetime Lifetime { get; } = lifetime;
    public Type? AsInterface { get; } = asInterface;
}
