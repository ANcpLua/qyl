using Microsoft.AspNetCore.Http;
using Qyl.Generated;
using qyl.mcp.Metadata;

namespace qyl.mcp.Hosting;

internal static class QylMcpLlmsTextBuilder
{
    public static string Create(McpHostOptions hostOptions, SkillConfiguration skills, HttpRequest request)
    {
        var capabilities = QylCapabilityCatalog.GetEnabled(skills);
        var builder = new StringBuilder();
        builder.AppendLine("# qyl MCP Server");
        builder.AppendLine();
        builder.AppendLine(QylServerMetadata.Summary);
        builder.AppendLine();
        builder.AppendLine($"- Endpoint: {hostOptions.ResolvePublicMcpUrl(request)}");
        builder.AppendLine("- Transport: Streamable HTTP");
        builder.AppendLine(
            $"- Auth: {(hostOptions.RequiresAuthentication ? "OAuth 2.0 bearer token" : "No host auth configured")}");
        var enabledToolCount = QylToolManifest.ToolDescriptors
            .Count(tool => tool.SkillKind is null || skills.IsEnabled(tool.SkillKind.Value));
        builder.AppendLine($"- Tool count: {enabledToolCount}");
        builder.AppendLine($"- Capability count: {capabilities.Count}");
        builder.AppendLine("- Discovery tools: `qyl.list_capabilities`, `qyl.get_capability_guide`");
        builder.AppendLine();
        builder.AppendLine("## Enabled Capabilities");

        foreach (var capability in capabilities)
            builder.AppendLine($"- {capability.Title} (`{capability.Id}`): {capability.Summary}");

        return builder.ToString();
    }
}
