namespace qyl.mcp.Capabilities;

internal enum QylCapabilityRole
{
    Starting,
    FollowUp
}

/// <summary>
///     Tags an MCP tool method as participating in a capability. The capability id must match
///     a <see cref="QylCapabilityDefinitionAttribute" /> in the same compilation — otherwise
///     the generator emits a diagnostic. Multiple capabilities may share a tool; a tool may
///     participate in multiple capabilities.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
internal sealed class QylCapabilityAttribute(
    string capabilityId,
    QylCapabilityRole role = QylCapabilityRole.Starting) : Attribute
{
    public string CapabilityId { get; } = capabilityId;
    public QylCapabilityRole Role { get; } = role;
}
