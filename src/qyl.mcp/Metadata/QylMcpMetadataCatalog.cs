namespace qyl.mcp.Metadata;

using Qyl.Generated;

/// <summary>
///     Thin filter wrapper around the generator-produced <c>QylToolManifest.ToolDescriptors</c>.
///     The descriptor list is compile-time: each entry already carries its skill from
///     <see cref="QylSkillAttribute" />, so no runtime enrichment layer is needed.
/// </summary>
internal static class QylMcpMetadataCatalog
{
    public static IReadOnlyList<ToolDescriptor> GetEnabledTools(SkillConfiguration skills) =>
    [
        .. QylToolManifest.ToolDescriptors
            .Where(tool => tool.SkillKind is null || skills.IsEnabled(tool.SkillKind.Value))
            .OrderBy(static tool => tool.Name, StringComparer.Ordinal)
    ];
}
