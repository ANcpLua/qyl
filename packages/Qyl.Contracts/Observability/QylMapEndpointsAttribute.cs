namespace Qyl.Contracts.Observability;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class QylMapEndpointsAttribute : Attribute
{
    public QylMapEndpointsAttribute(int order = 100) => Order = order;

    public int Order { get; }
}
