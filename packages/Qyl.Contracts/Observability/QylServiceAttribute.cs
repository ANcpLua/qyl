namespace Qyl.Contracts.Observability;

public enum QylLifetime
{
    Singleton,
    Scoped,
    Transient
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class QylServiceAttribute(QylLifetime Lifetime, Type? AsInterface = null) : Attribute
{
    public QylLifetime Lifetime { get; } = Lifetime;
    public Type? AsInterface { get; } = AsInterface;
}
