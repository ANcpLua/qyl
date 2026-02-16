using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools;

/// <summary>
///     MCP tools for querying workspace identity and configuration.
/// </summary>
[McpServerToolType]
public sealed class WorkspaceTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.get_workspace")]
    [Description("""
                 Get workspace identity and configuration info.

                 Returns workspace details including:
                 - Workspace name and ID
                 - Owner and members
                 - Configuration settings
                 - Data retention policy
                 - Storage usage

                 Use this to understand the current workspace context.

                 Returns: Workspace identity and configuration summary
                 """)]
    public Task<string> GetWorkspaceAsync(
        [Description("Workspace ID to look up (default: current workspace)")]
        string? workspaceId = null) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var url = string.IsNullOrEmpty(workspaceId)
                ? "/api/v1/workspace"
                : $"/api/v1/workspaces/{Uri.EscapeDataString(workspaceId)}";

            var workspace = await client.GetFromJsonAsync<WorkspaceDto>(
                url, WorkspaceJsonContext.Default.WorkspaceDto).ConfigureAwait(false);

            if (workspace is null)
            {
                return workspaceId is not null
                    ? $"Workspace '{workspaceId}' not found."
                    : "No workspace information available.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# Workspace: {workspace.Name ?? "unnamed"}");
            sb.AppendLine();
            sb.AppendLine($"- **ID:** {workspace.WorkspaceId}");

            if (!string.IsNullOrEmpty(workspace.Owner))
                sb.AppendLine($"- **Owner:** {workspace.Owner}");

            if (!string.IsNullOrEmpty(workspace.Plan))
                sb.AppendLine($"- **Plan:** {workspace.Plan}");

            if (workspace.RetentionDays > 0)
                sb.AppendLine($"- **Retention:** {workspace.RetentionDays} days");

            if (!string.IsNullOrEmpty(workspace.Region))
                sb.AppendLine($"- **Region:** {workspace.Region}");

            if (!string.IsNullOrEmpty(workspace.CreatedAt))
                sb.AppendLine($"- **Created:** {workspace.CreatedAt}");

            if (workspace.MemberCount > 0)
                sb.AppendLine($"- **Members:** {workspace.MemberCount}");

            if (workspace.StorageUsedBytes > 0)
            {
                var sizeMb = workspace.StorageUsedBytes / (1024.0 * 1024.0);
                sb.AppendLine($"- **Storage used:** {sizeMb:F1} MB");
            }

            return sb.ToString();
        });
}

#region DTOs

internal sealed record WorkspaceDto(
    [property: JsonPropertyName("workspace_id")]
    string WorkspaceId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("owner")] string? Owner,
    [property: JsonPropertyName("plan")] string? Plan,
    [property: JsonPropertyName("retention_days")]
    int RetentionDays,
    [property: JsonPropertyName("region")] string? Region,
    [property: JsonPropertyName("created_at")]
    string? CreatedAt,
    [property: JsonPropertyName("member_count")]
    int MemberCount,
    [property: JsonPropertyName("storage_used_bytes")]
    long StorageUsedBytes);

#endregion

[JsonSerializable(typeof(WorkspaceDto))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class WorkspaceJsonContext : JsonSerializerContext;
