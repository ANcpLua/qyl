using qyl.mcp.Skills;
using Qyl.Generated;

namespace qyl.mcp.Metadata;

internal static class QylMcpMetadataCatalog
{
    private static readonly Lazy<IReadOnlyList<RuntimeToolDescriptor>> AllTools = new(BuildAllTools);

    public static IReadOnlyList<RuntimeToolDescriptor> GetAllTools() => AllTools.Value;

    public static IReadOnlyList<RuntimeToolDescriptor> GetEnabledTools(SkillConfiguration skills) =>
        [.. GetAllTools().Where(tool => QylSkillCatalog.IsEnabled(tool.DeclaringType, skills))];

    private static IReadOnlyList<RuntimeToolDescriptor> BuildAllTools() =>
        [..
            QylToolManifest.ToolDescriptors
                .Select(static descriptor => new RuntimeToolDescriptor
                {
                    Name = descriptor.Name,
                    MethodName = descriptor.MethodName,
                    DeclaringType = descriptor.DeclaringType,
                    Skill = QylSkillCatalog.GetSkillLabel(descriptor.DeclaringType),
                    Title = descriptor.Title,
                    Description = descriptor.Description,
                    ReadOnly = descriptor.ReadOnly,
                    Destructive = descriptor.Destructive,
                    Idempotent = descriptor.Idempotent,
                    OpenWorld = descriptor.OpenWorld,
                    ReturnType = descriptor.ReturnType,
                })
                .OrderBy(static descriptor => descriptor.Name, StringComparer.Ordinal)
        ];
}
