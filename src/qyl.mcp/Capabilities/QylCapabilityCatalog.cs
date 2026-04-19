using Qyl.Generated;

namespace qyl.mcp.Capabilities;

/// <summary>
///     Thin filter wrapper around the generator-produced capability list in
///     <c>QylToolManifest.Capabilities</c>. The actual capability metadata is defined via
///     <see cref="QylCapabilityDefinitionAttribute" /> markers and assembled at compile time.
/// </summary>
internal static class QylCapabilityCatalog
{
    public static IReadOnlyList<QylCapabilityDescriptor> GetEnabled(SkillConfiguration skills) =>
        [.. QylToolManifest.Capabilities
            .Where(capability => capability.RequiredSkill is null || skills.IsEnabled(capability.RequiredSkill.Value))
            .OrderBy(static capability => capability.Title, StringComparer.Ordinal)];

    public static QylCapabilityDescriptor? FindEnabled(string capabilityId, SkillConfiguration skills) =>
        GetEnabled(skills).FirstOrDefault(capability => capability.Id.EqualsIgnoreCase(capabilityId));
}
