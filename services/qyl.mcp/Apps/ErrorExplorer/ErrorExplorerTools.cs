using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ANcpLua.Roslyn.Utilities.Web;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using qyl.mcp.Tools;

namespace qyl.mcp.Apps.ErrorExplorer;

[McpServerToolType]
[QylSkill(QylSkillKind.Apps)]
public sealed partial class ErrorExplorerTools(HttpClient client)
{
    private const string AppUri = "ui://qyl/error-explorer";

    [QylCapability("mcp_apps")]
    [McpServerTool(Name = "qyl.app.error_explorer", Title = "Error Explorer",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
    public async partial Task<CallToolResult> ExploreErrorsAsync(
        string? status = null,
        string? service = null,
        int limit = 50)
    {
        var text = await CollectorHelper.ExecuteAsync(async () =>
        {
            var url = QueryString.AppendPairs(
                $"/api/v1/issues?limit={limit}",
                ("status", status), ("service", service));

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
