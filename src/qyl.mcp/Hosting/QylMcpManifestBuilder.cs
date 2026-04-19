using Microsoft.AspNetCore.Http;
using qyl.mcp.Metadata;

namespace qyl.mcp.Hosting;

internal static class QylMcpManifestBuilder
{
    public static QylMcpManifest Create(HttpRequest request, McpHostOptions hostOptions, SkillConfiguration skills)
    {
        var capabilities = QylCapabilityCatalog.GetEnabled(skills)
            .Select(static capability => new QylMcpManifestCapability
            {
                Id = capability.Id,
                Title = capability.Title,
                Summary = capability.Summary,
                Skill = capability.Skill,
            })
            .ToArray();

        return new QylMcpManifest
        {
            Name = QylServerMetadata.Name,
            Version = QylServerMetadata.Version,
            Endpoint = hostOptions.ResolvePublicMcpUrl(request),
            Transport = "streamable-http",
            Auth = hostOptions.RequiresAuthentication ? "oauth2-bearer" : "none",
            Summary = QylServerMetadata.Summary,
            ToolCount = QylMcpMetadataCatalog.GetEnabledTools(skills).Count,
            CapabilityCount = capabilities.Length,
            ToolFamilies = QylSkillCatalog.GetEnabledSkillLabels(skills),
            Capabilities = capabilities,
        };
    }
}

internal sealed record QylMcpManifest
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Endpoint { get; init; }
    public required string Transport { get; init; }
    public required string Auth { get; init; }
    public required string Summary { get; init; }
    public required int ToolCount { get; init; }
    public required int CapabilityCount { get; init; }
    public required IReadOnlyList<string> ToolFamilies { get; init; }
    public required IReadOnlyList<QylMcpManifestCapability> Capabilities { get; init; }
}

internal sealed record QylMcpManifestCapability
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string Skill { get; init; }
}
