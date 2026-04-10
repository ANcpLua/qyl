using System.Text;
using Microsoft.AspNetCore.Http;
using qyl.mcp.Capabilities;
using qyl.mcp.Metadata;
using qyl.mcp.Skills;

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
        builder.AppendLine($"- Auth: {(hostOptions.RequiresAuthentication ? "OAuth 2.0 bearer token" : "No host auth configured")}");
        builder.AppendLine($"- Tool count: {QylMcpMetadataCatalog.GetEnabledTools(skills).Count}");
        builder.AppendLine($"- Capability count: {capabilities.Count}");
        builder.AppendLine("- Discovery tools: `qyl.list_capabilities`, `qyl.get_capability_guide`");
        builder.AppendLine();
        builder.AppendLine("## Enabled Capabilities");

        foreach (var capability in capabilities)
            builder.AppendLine($"- {capability.Title} (`{capability.Id}`): {capability.Summary}");

        return builder.ToString();
    }
}
