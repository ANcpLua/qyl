using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using qyl.mcp.Metadata;
using qyl.mcp.Skills;

namespace qyl.mcp.Capabilities;

[McpServerToolType]
internal sealed class CapabilityTools(SkillConfiguration skills)
{
    [QylCapability("server_introspection", QylCapabilityRole.Starting)]
    [McpServerTool(Name = "qyl.list_capabilities", Title = "List Capabilities",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List qyl.mcp capabilities, enabled skill families, and the primary tools behind each capability. Use this before invoking broad meta-agent tools.")]
    public string ListCapabilities(
        [Description("Optional skill family filter such as inspect, health, analytics, agent, build, anomaly, loom, apps, debug, or core")]
        string? skill = null,
        [Description("Optional tag filter such as traces, errors, logs, metrics, genai, apps, or debugger")]
        string? tag = null,
        [Description("Include the primary tool names for each capability in the response")]
        bool includeTools = false)
    {
        var capabilities = QylCapabilityCatalog.GetEnabled(skills)
            .Where(capability => skill is null || capability.Skill.EqualsIgnoreCase(skill))
            .Where(capability => tag is null || capability.Tags.Any(candidate => candidate.EqualsIgnoreCase(tag)))
            .Select(capability => new CapabilityListItemDto
            {
                Id = capability.Id,
                Title = capability.Title,
                Summary = capability.Summary,
                Skill = capability.Skill,
                Tags = capability.Tags,
                ToolNames = includeTools ? capability.ToolNames : null,
            })
            .ToArray();

        var response = new CapabilityListResponseDto
        {
            Server = QylServerMetadata.Name,
            Version = QylServerMetadata.Version,
            Skills = QylSkillCatalog.GetEnabledSkillLabels(skills),
            CapabilityCount = capabilities.Length,
            Capabilities = capabilities,
        };

        return JsonSerializer.Serialize(response, CapabilityToolsJsonContext.Default.CapabilityListResponseDto);
    }

    [QylCapability("server_introspection", QylCapabilityRole.Starting)]
    [McpServerTool(Name = "qyl.get_capability_guide", Title = "Get Capability Guide",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Return a detailed qyl-specific guide for one capability, including identifiers, recommended first tools, follow-up tools, scoping advice, and telemetry evidence hints.")]
    public string GetCapabilityGuide(
        [Description("Capability id from qyl.list_capabilities, for example trace_investigation or loom_triage_and_fix")]
        string capabilityId)
    {
        var capability = QylCapabilityCatalog.FindEnabled(capabilityId, skills);
        if (capability is null)
        {
            var notFound = new CapabilityGuideResponseDto
            {
                Found = false,
                CapabilityId = capabilityId,
                Message = "Capability not found or not enabled by the current skill configuration.",
                AvailableCapabilities = QylCapabilityCatalog.GetEnabled(skills)
                    .Select(static candidate => candidate.Id)
                    .ToArray(),
            };

            return JsonSerializer.Serialize(notFound, CapabilityToolsJsonContext.Default.CapabilityGuideResponseDto);
        }

        var enabledToolMap = QylMcpMetadataCatalog.GetEnabledTools(skills)
            .ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);

        var response = new CapabilityGuideResponseDto
        {
            Found = true,
            CapabilityId = capability.Id,
            Title = capability.Title,
            Summary = capability.Summary,
            Skill = capability.Skill,
            Tags = capability.Tags,
            UseCases = capability.UseCases,
            PrimaryIdentifiers = capability.PrimaryIdentifiers,
            StartingTools = capability.StartingTools,
            FollowUpTools = capability.FollowUpTools,
            ScopingHints = capability.ScopingHints,
            EvidenceHints = capability.EvidenceHints,
            RelatedCapabilities = capability.RelatedCapabilityIds,
            Tools = capability.ToolNames
                .Where(enabledToolMap.ContainsKey)
                .Select(name =>
                {
                    var tool = enabledToolMap[name];
                    return new CapabilityToolReferenceDto
                    {
                        Name = tool.Name,
                        Title = tool.Title,
                        Skill = tool.Skill,
                        ReadOnly = tool.ReadOnly,
                        Destructive = tool.Destructive,
                    };
                })
                .ToArray(),
        };

        return JsonSerializer.Serialize(response, CapabilityToolsJsonContext.Default.CapabilityGuideResponseDto);
    }
}

internal sealed record CapabilityListResponseDto
{
    public required string Server { get; init; }
    public required string Version { get; init; }
    public required IReadOnlyList<string> Skills { get; init; }
    public required int CapabilityCount { get; init; }
    public required IReadOnlyList<CapabilityListItemDto> Capabilities { get; init; }
}

internal sealed record CapabilityListItemDto
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string Skill { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public IReadOnlyList<string>? ToolNames { get; init; }
}

internal sealed record CapabilityGuideResponseDto
{
    public required bool Found { get; init; }
    public required string CapabilityId { get; init; }
    public string? Message { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public string? Skill { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public IReadOnlyList<string>? UseCases { get; init; }
    public IReadOnlyList<string>? PrimaryIdentifiers { get; init; }
    public IReadOnlyList<string>? StartingTools { get; init; }
    public IReadOnlyList<string>? FollowUpTools { get; init; }
    public IReadOnlyList<string>? ScopingHints { get; init; }
    public IReadOnlyList<string>? EvidenceHints { get; init; }
    public IReadOnlyList<string>? RelatedCapabilities { get; init; }
    public IReadOnlyList<CapabilityToolReferenceDto>? Tools { get; init; }
    public IReadOnlyList<string>? AvailableCapabilities { get; init; }
}

internal sealed record CapabilityToolReferenceDto
{
    public required string Name { get; init; }
    public string? Title { get; init; }
    public required string Skill { get; init; }
    public required bool ReadOnly { get; init; }
    public required bool Destructive { get; init; }
}

[JsonSerializable(typeof(CapabilityListResponseDto))]
[JsonSerializable(typeof(CapabilityGuideResponseDto))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class CapabilityToolsJsonContext : JsonSerializerContext;
