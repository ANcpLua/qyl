using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using qyl.mcp.Tools;

namespace qyl.mcp.Apps.ErrorExplorer;

/// <summary>
///     MCP ext-app tool that returns error groups with a UI resource link.
///     Detail/timeline queries reuse existing <see cref="ErrorTools" />.
/// </summary>
[McpServerToolType]
[QylSkill(QylSkillKind.Apps)]
public sealed class ErrorExplorerTools(HttpClient client)
{
    private const string AppUri = "ui://qyl/error-explorer";

    /// <summary>
    ///     Returns error groups as Markdown text and signals the interactive Error Explorer UI resource.
    /// </summary>
    /// <param name="status">Filter by issue status: unresolved, resolved, or ignored.</param>
    /// <param name="service">Filter by originating service name.</param>
    /// <param name="limit">Maximum number of error groups to return.</param>
    [QylCapability("mcp_apps", QylCapabilityRole.Starting)]
    [McpServerTool(Name = "qyl.app.error_explorer", Title = "Error Explorer",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("""
                 Open the Error Explorer interactive UI showing grouped error issues.
                 Returns error groups as text AND signals the interactive UI resource.

                 Example: error_explorer(status="unresolved", limit=50)
                 """)]
    public async Task<CallToolResult> ExploreErrorsAsync(
        [Description("Filter by status: 'unresolved', 'resolved', 'ignored'")]
        string? status = null,
        [Description("Filter by service name")]
        string? service = null,
        [Description("Maximum error groups (default: 50)")]
        int limit = 50)
    {
        var text = await CollectorHelper.ExecuteAsync(async () =>
        {
            var url = $"/api/v1/issues?limit={limit}";
            if (!string.IsNullOrEmpty(status))
                url += $"&status={Uri.EscapeDataString(status)}";
            if (!string.IsNullOrEmpty(service))
                url += $"&service={Uri.EscapeDataString(service)}";

            var response = await client.GetFromJsonAsync<ErrorIssueListResponse>(
                url, ErrorJsonContext.Default.ErrorIssueListResponse).ConfigureAwait(false);

            if (response?.Items is not { Count: > 0 })
                return "No error groups found.";

            StringBuilder sb = new();
            sb.AppendLine($"# Error Explorer ({response.Items.Count} of {response.Total})");
            sb.AppendLine();
            sb.AppendLine("| Status | Type | Title | Count | Last Seen |");
            sb.AppendLine("|--------|------|-------|-------|-----------|");

            foreach (var issue in response.Items)
            {
                var title = issue.Title.Length > 50
                    ? string.Concat(issue.Title.AsSpan(0, 47), "...")
                    : issue.Title;
                sb.AppendLine(
                    $"| {issue.Status} | {issue.ErrorType} | {title} | {issue.OccurrenceCount:N0} | {issue.LastSeenAt:yyyy-MM-dd HH:mm} |");
            }

            return sb.ToString();
        }).ConfigureAwait(false);

        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock { Text = text },
                new ResourceLinkBlock
                {
                    Uri = AppUri,
                    Name = "Error Explorer",
                    Title = "Interactive Error Dashboard",
                    MimeType = "text/html;profile=mcp-app"
                }
            ],
            Meta = new JsonObject { ["ui"] = new JsonObject { ["resourceUri"] = AppUri } }
        };
    }
}

[JsonSerializable(typeof(CallToolResult))]
[JsonSerializable(typeof(TextContentBlock))]
[JsonSerializable(typeof(ResourceLinkBlock))]
[JsonSerializable(typeof(JsonObject))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class ErrorExplorerJsonContext : JsonSerializerContext;
