namespace qyl.mcp.Capabilities;

internal enum QylCapabilityRole
{
    Starting,
    FollowUp
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
internal sealed class QylCapabilityAttribute(
    string capabilityId,
    QylCapabilityRole role = QylCapabilityRole.Starting) : Attribute
{
    public string CapabilityId { get; } = capabilityId;
    public QylCapabilityRole Role { get; } = role;
}
